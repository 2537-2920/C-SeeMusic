using System.Globalization;
using System.Text;
using backend.Models;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class PianoTranscriptionService : IPianoTranscriptionService
{
    private const int TargetSampleRate = 11025;
    private const double MinimumPitchHz = 80.0;
    private const double MaximumPitchHz = 1200.0;
    private static readonly string[] SharpPitchNames =
    {
        "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
    };

    private static readonly double[] MajorProfile =
    {
        6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88
    };

    private static readonly double[] MinorProfile =
    {
        6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17
    };

    private readonly IBeatAnalysisService _beatAnalysisService;
    private readonly TranscriptionProcessingOptions _processingOptions;

    public PianoTranscriptionService(
        IBeatAnalysisService beatAnalysisService,
        IOptions<TranscriptionProcessingOptions> processingOptions)
    {
        _beatAnalysisService = beatAnalysisService;
        _processingOptions = processingOptions.Value ?? new TranscriptionProcessingOptions();
    }

    public PianoTranscriptionResult Transcribe(string preparedAudioPath, string title, TranscriptionOptionsRequest options)
    {
        if (string.IsNullOrWhiteSpace(preparedAudioPath) || !File.Exists(preparedAudioPath))
        {
            return new PianoTranscriptionResult
            {
                Status = "failed",
                ErrorMessage = "未找到可识谱的标准音频文件。"
            };
        }

        var normalizedOptions = (options ?? new TranscriptionOptionsRequest()).Normalize();
        if (!string.Equals(normalizedOptions.Mode, "piano", StringComparison.OrdinalIgnoreCase))
        {
            return new PianoTranscriptionResult
            {
                Status = "failed",
                ErrorMessage = "当前识谱流程仅支持钢琴模式。"
            };
        }

        if (!normalizedOptions.SeparateMelody)
        {
            return new PianoTranscriptionResult
            {
                Status = "failed",
                ErrorMessage = "当前钢琴识谱必须启用旋律提取。"
            };
        }

        try
        {
            var beatAnalysis = normalizedOptions.AnalyzeRhythm
                ? _beatAnalysisService.AnalyzeFile(preparedAudioPath)
                : CreateDisabledBeatAnalysis();
            var audioData = WavAudioReader.Read(preparedAudioPath);
            var contour = ExtractPitchContour(audioData);
            if (contour.Count < 8)
            {
                return new PianoTranscriptionResult
                {
                    Status = "failed",
                    BeatAnalysis = beatAnalysis,
                    ErrorMessage = "未提取到足够稳定的旋律轮廓，当前无法生成钢琴谱。",
                    Warnings =
                    {
                        "请尝试上传更清晰的旋律主导音频，或减少背景伴奏干扰。"
                    }
                };
            }

            var warnings = new List<string>();
            var rhythmGrid = ResolveRhythmGrid(beatAnalysis, contour, normalizedOptions.AnalyzeRhythm, warnings);
            var beatDurationSeconds = rhythmGrid.BeatDurationSeconds;
            var timeSignatureNumerator = rhythmGrid.TimeSignatureNumerator;
            var tempoBpm = rhythmGrid.TempoBpm;
            var quantization = rhythmGrid.Quantization;

            var keyDetection = DetectKey(contour);
            if (!keyDetection.IsReliable)
            {
                warnings.Add("调性识别置信度有限，当前已按接近主旋律中心的规则生成伴奏。");
            }

            var melodyNotes = BuildMelodyNotes(contour, beatAnalysis, beatDurationSeconds, timeSignatureNumerator, quantization);
            if (melodyNotes.Count < 4)
            {
                return new PianoTranscriptionResult
                {
                    Status = "failed",
                    BeatAnalysis = beatAnalysis,
                    ErrorMessage = "旋律片段过少，当前无法稳定生成双手钢琴谱。",
                    Warnings = warnings
                };
            }

            var accompanimentNotes = normalizedOptions.SeparateAccompaniment
                ? BuildAccompanimentNotes(melodyNotes, keyDetection, timeSignatureNumerator)
                : new List<GeneratedNoteResult>();
            if (!normalizedOptions.SeparateAccompaniment)
            {
                warnings.Add("当前已关闭左手伴奏自动编配，本次结果只保留右手旋律轨道。");
            }

            var measureCount = Math.Max(
                melodyNotes.Max(note => note.MeasureNo),
                accompanimentNotes.Count == 0 ? 1 : accompanimentNotes.Max(note => note.MeasureNo));

            var tracks = new List<GeneratedTrackResult>
            {
                BuildTrack("右手旋律", "right", false, "extracted_melody", melodyNotes, "根据稳定音高轮廓切分并量化生成右手旋律。"),
            };

            if (normalizedOptions.SeparateAccompaniment)
            {
                tracks.Add(BuildTrack(
                    "左手伴奏",
                    "left",
                    true,
                    "arranged_accompaniment",
                    accompanimentNotes,
                    timeSignatureNumerator == 3
                        ? "按 3/4 规则生成低音加两拍支撑的左手伴奏。"
                        : timeSignatureNumerator == 2
                            ? "按 2/4 规则生成低音与支撑和声的左手伴奏。"
                            : "按 4/4 规则生成低音与分解和弦结合的左手伴奏。"));
            }

            var titleText = string.IsNullOrWhiteSpace(title) ? "我的智能识谱项目" : title.Trim();
            var keySignature = FormatKeySignature(keyDetection);
            var musicXml = MusicXmlBuilder.Build(titleText, timeSignatureNumerator, 4, tempoBpm, keySignature, tracks);
            var estimatedPageCount = ScorePreviewRenderer.CalculatePageCount(
                measureCount,
                _processingOptions.MeasuresPerSystem,
                _processingOptions.SystemsPerPage);

            return new PianoTranscriptionResult
            {
                Status = "succeeded",
                BeatAnalysis = BuildOutputBeatAnalysis(beatAnalysis, rhythmGrid, warnings),
                KeySignature = keySignature,
                KeyConfidence = Math.Round(keyDetection.Confidence, 3),
                MusicXmlContent = musicXml,
                MeasureCount = measureCount,
                EstimatedPageCount = estimatedPageCount,
                PreviewRenderMode = "generated_svg_projection",
                TrackBuildMode = normalizedOptions.SeparateAccompaniment
                    ? "melody_extraction_with_arranged_accompaniment"
                    : "melody_extraction_only",
                AnalysisSummary = new ScoreAnalysisSummaryResponse
                {
                    MelodySummary = $"右手共生成 {melodyNotes.Count} 个旋律音符，量化粒度为 {(quantization < 0.5 ? "1/16" : "1/8")}。",
                    AccompanimentSummary = accompanimentNotes.Count == 0
                        ? "本次未生成左手伴奏。"
                        : $"左手共生成 {accompanimentNotes.Count} 个伴奏音符，基于 {keySignature} 调性中心规则生成。",
                    AssignmentSummary = "高音区与主旋律优先分配到右手，低音区与和声支撑分配到左手。",
                    TrackBuildSummary = normalizedOptions.SeparateAccompaniment
                        ? "当前结果采用“旋律提取 + 左手规则编配”的双手钢琴重构流程。"
                        : "当前结果采用“旋律提取”流程，仅输出右手主旋律轨道。",
                    KeyConfidence = Math.Round(keyDetection.Confidence, 3),
                    RhythmSummary = rhythmGrid.Summary
                },
                Tracks = tracks,
                Warnings = warnings
            };
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return new PianoTranscriptionResult
            {
                Status = "failed",
                ErrorMessage = exception.Message
            };
        }
    }

    private static GeneratedTrackResult BuildTrack(
        string name,
        string handRole,
        bool isGenerated,
        string origin,
        List<GeneratedNoteResult> notes,
        string summaryText)
    {
        return new GeneratedTrackResult
        {
            Name = name,
            HandRole = handRole,
            Instrument = "piano",
            IsGenerated = isGenerated,
            Origin = origin,
            SummaryText = summaryText,
            Notes = notes
        };
    }

    private static ResolvedRhythmGrid ResolveRhythmGrid(
        BeatAnalysisResult beatAnalysis,
        IReadOnlyList<PitchContourPoint> contour,
        bool analyzeRhythm,
        ICollection<string> warnings)
    {
        if (!analyzeRhythm)
        {
            warnings.Add("已关闭节拍识别，当前结果按默认 96 BPM / 4/4 节拍网格量化。");
            return new ResolvedRhythmGrid(60.0 / 96.0, 4, 96.0, 0.5, "disabled", 0.0, "已关闭节拍识别，当前按默认 96 BPM / 4/4 节拍网格生成。");
        }

        if (beatAnalysis.IsAvailable && beatAnalysis.TempoBpm > 0)
        {
            return new ResolvedRhythmGrid(
                60.0 / beatAnalysis.TempoBpm,
                Math.Clamp(beatAnalysis.TimeSignatureNumerator, 2, 4),
                Math.Max(60.0, beatAnalysis.TempoBpm),
                beatAnalysis.Stability >= 0.75 && beatAnalysis.Confidence >= 0.55 ? 0.25 : 0.5,
                string.IsNullOrWhiteSpace(beatAnalysis.GridSource) ? "detected" : beatAnalysis.GridSource,
                beatAnalysis.TimeSignatureConfidence,
                beatAnalysis.Summary);
        }

        if (contour.Count > 6)
        {
            var noteLikeGaps = new List<double>();
            for (var index = 1; index < contour.Count; index++)
            {
                var gap = contour[index].TimeSeconds - contour[index - 1].TimeSeconds;
                if (gap >= 0.18 && gap <= 1.0)
                {
                    noteLikeGaps.Add(gap);
                }
            }

            if (noteLikeGaps.Count > 2)
            {
                warnings.Add("未检测到可靠拍点，已根据旋律停顿估算节拍网格。");
                var estimatedBeatDuration = Math.Clamp(noteLikeGaps.OrderBy(value => value).ElementAt(noteLikeGaps.Count / 2), 0.35, 0.8);
                return new ResolvedRhythmGrid(
                    estimatedBeatDuration,
                    4,
                    Math.Round(60.0 / estimatedBeatDuration, 1),
                    0.5,
                    "pause_estimated",
                    0.0,
                    "未检测到可靠拍点，已根据旋律停顿估算节拍网格。");
            }
        }

        warnings.Add("未检测到可靠拍点，已使用默认 96 BPM / 4/4 节拍网格生成结果。");
        return new ResolvedRhythmGrid(60.0 / 96.0, 4, 96.0, 0.5, "default_fallback", 0.0, "未检测到可靠拍点，已使用默认 96 BPM / 4/4 节拍网格生成结果。");
    }

    private static BeatAnalysisResult CreateDisabledBeatAnalysis()
    {
        return new BeatAnalysisResult
        {
            IsAvailable = false,
            TempoBpm = 96.0,
            TimeSignatureNumerator = 4,
            TimeSignatureDenominator = 4,
            GridSource = "disabled",
            Summary = "已关闭节拍识别，当前按默认 96 BPM / 4/4 节拍网格生成。"
        };
    }

    private static BeatAnalysisResult BuildOutputBeatAnalysis(
        BeatAnalysisResult beatAnalysis,
        ResolvedRhythmGrid rhythmGrid,
        IReadOnlyCollection<string> warnings)
    {
        if (beatAnalysis.IsAvailable)
        {
            beatAnalysis.GridSource = string.IsNullOrWhiteSpace(beatAnalysis.GridSource) ? rhythmGrid.GridSource : beatAnalysis.GridSource;
            beatAnalysis.TimeSignatureConfidence = beatAnalysis.TimeSignatureConfidence <= 0
                ? rhythmGrid.TimeSignatureConfidence
                : beatAnalysis.TimeSignatureConfidence;
            return beatAnalysis;
        }

        return new BeatAnalysisResult
        {
            IsAvailable = false,
            TempoBpm = rhythmGrid.TempoBpm,
            TimeSignatureNumerator = rhythmGrid.TimeSignatureNumerator,
            TimeSignatureDenominator = 4,
            TimeSignatureConfidence = rhythmGrid.TimeSignatureConfidence,
            GridSource = rhythmGrid.GridSource,
            Summary = warnings.Count == 0 ? rhythmGrid.Summary : warnings.First()
        };
    }

    private static List<PitchContourPoint> ExtractPitchContour(WavAudioData audioData)
    {
        var downsampled = Downsample(audioData.Samples, audioData.SampleRate, out var sampleRate);
        var normalized = NormalizeSamples(downsampled);
        const int frameSize = 1024;
        const int hopSize = 256;
        const double minimumEnergy = 0.018;
        const double minimumCorrelation = 0.52;

        var points = new List<PitchContourPoint>();
        for (var frameStart = 0; frameStart + frameSize < normalized.Length; frameStart += hopSize)
        {
            var pitch = DetectPitch(normalized, frameStart, frameSize, sampleRate, minimumEnergy, minimumCorrelation);
            if (pitch == null)
            {
                continue;
            }

            var time = (frameStart + frameSize / 2.0) / sampleRate;
            points.Add(new PitchContourPoint(time, pitch.Value));
        }

        return SmoothPoints(points, 3);
    }

    private static List<GeneratedNoteResult> BuildMelodyNotes(
        IReadOnlyList<PitchContourPoint> contour,
        BeatAnalysisResult beatAnalysis,
        double beatDurationSeconds,
        int timeSignatureNumerator,
        double quantization)
    {
        var segments = new List<List<PitchContourPoint>>();
        var current = new List<PitchContourPoint>();

        foreach (var point in contour)
        {
            if (current.Count == 0)
            {
                current.Add(point);
                continue;
            }

            var previous = current[^1];
            var pitchDelta = Math.Abs(FrequencyToMidi(point.FrequencyHz) - FrequencyToMidi(previous.FrequencyHz));
            var timeGap = point.TimeSeconds - previous.TimeSeconds;

            if (timeGap > 0.18 || pitchDelta > 1.35)
            {
                if (current.Count > 0)
                {
                    segments.Add(current);
                }

                current = new List<PitchContourPoint>();
            }

            current.Add(point);
        }

        if (current.Count > 0)
        {
            segments.Add(current);
        }

        var beatOrigin = beatAnalysis.IsAvailable && beatAnalysis.BeatTimes.Count > 0
            ? beatAnalysis.BeatTimes[0]
            : 0.0;
        var notes = new List<GeneratedNoteResult>();

        foreach (var segment in segments)
        {
            if (segment.Count < 2)
            {
                continue;
            }

            var startTime = segment.First().TimeSeconds;
            var endTime = segment.Last().TimeSeconds + 0.08;
            var rawDurationBeats = Math.Max(quantization, (endTime - startTime) / beatDurationSeconds);
            var absoluteBeat = Math.Max(0.0, (startTime - beatOrigin) / beatDurationSeconds);
            var quantizedBeat = Quantize(absoluteBeat, quantization);
            var durationBeats = Quantize(rawDurationBeats, quantization);
            var remainingInMeasure = timeSignatureNumerator - (quantizedBeat % timeSignatureNumerator);
            if (remainingInMeasure <= 0.001)
            {
                remainingInMeasure = timeSignatureNumerator;
            }

            durationBeats = Math.Max(quantization, Math.Min(durationBeats, remainingInMeasure));
            var normalizedDuration = NormalizeDuration(durationBeats);
            var midiValues = segment.Select(point => FrequencyToMidi(point.FrequencyHz)).OrderBy(value => value).ToArray();
            var midiNumber = (int)Math.Round(midiValues[midiValues.Length / 2]);
            if (midiNumber < 55 || midiNumber > 96)
            {
                continue;
            }

            var measureNo = (int)Math.Floor(quantizedBeat / timeSignatureNumerator) + 1;
            var beatInMeasure = quantizedBeat % timeSignatureNumerator;
            if (notes.Count > 0)
            {
                var previous = notes[^1];
                if (previous.MeasureNo == measureNo && Math.Abs(previous.BeatStart - beatInMeasure) < 0.001)
                {
                    if (previous.DurationBeats < normalizedDuration.Beats)
                    {
                        notes[^1] = CreateNote(previous.MeasureNo, previous.BeatStart, normalizedDuration, midiNumber, "treble", startTime, false);
                    }

                    continue;
                }
            }

            notes.Add(CreateNote(measureNo, beatInMeasure, normalizedDuration, midiNumber, "treble", startTime, false));
        }

        return notes
            .Where(note => note.MeasureNo > 0)
            .OrderBy(note => note.MeasureNo)
            .ThenBy(note => note.BeatStart)
            .ThenBy(note => note.MidiNumber)
            .ToList();
    }

    private static List<GeneratedNoteResult> BuildAccompanimentNotes(
        IReadOnlyList<GeneratedNoteResult> melodyNotes,
        KeyDetectionResult keyDetection,
        int timeSignatureNumerator)
    {
        if (melodyNotes.Count == 0)
        {
            return new List<GeneratedNoteResult>();
        }

        var lastMeasure = melodyNotes.Max(note => note.MeasureNo);
        var globalRoot = keyDetection.RootPitchClass;
        var isMinor = string.Equals(keyDetection.Mode, "minor", StringComparison.OrdinalIgnoreCase);
        var notes = new List<GeneratedNoteResult>();

        for (var measure = 1; measure <= lastMeasure; measure++)
        {
            var melodyInMeasure = melodyNotes.Where(note => note.MeasureNo == measure).ToList();
            var chordRoot = ResolveMeasureChordRoot(globalRoot, isMinor, melodyInMeasure);
            var bassRootMidi = FindClosestMidi(chordRoot, 45);
            var thirdMidi = FindClosestMidi((chordRoot + (isMinor ? 3 : 4)) % 12, 53);
            var fifthMidi = FindClosestMidi((chordRoot + 7) % 12, 57);

            if (timeSignatureNumerator == 2)
            {
                notes.Add(CreateNote(measure, 0, DurationToken.Quarter, bassRootMidi, "bass", (measure - 1) * 2, false));
                notes.Add(CreateNote(measure, 1, DurationToken.Quarter, thirdMidi, "bass", (measure - 1) * 2 + 1, true));
                continue;
            }

            if (timeSignatureNumerator == 3)
            {
                notes.Add(CreateNote(measure, 0, DurationToken.Quarter, bassRootMidi, "bass", (measure - 1) * 3, false));
                notes.Add(CreateNote(measure, 1, DurationToken.Quarter, thirdMidi, "bass", (measure - 1) * 3 + 1, true));
                notes.Add(CreateNote(measure, 2, DurationToken.Quarter, fifthMidi, "bass", (measure - 1) * 3 + 2, true));
                continue;
            }

            notes.Add(CreateNote(measure, 0, DurationToken.Quarter, bassRootMidi, "bass", (measure - 1) * 4, false));
            notes.Add(CreateNote(measure, 1, DurationToken.Quarter, thirdMidi, "bass", (measure - 1) * 4 + 1, true));
            notes.Add(CreateNote(measure, 2, DurationToken.Quarter, fifthMidi, "bass", (measure - 1) * 4 + 2, true));
            notes.Add(CreateNote(measure, 3, DurationToken.Quarter, thirdMidi + 12 <= 67 ? thirdMidi + 12 : thirdMidi, "bass", (measure - 1) * 4 + 3, true));
        }

        return notes;
    }

    private static int ResolveMeasureChordRoot(int globalRootPitchClass, bool isMinor, IReadOnlyList<GeneratedNoteResult> melodyInMeasure)
    {
        if (melodyInMeasure.Count == 0)
        {
            return globalRootPitchClass;
        }

        var candidates = new[]
        {
            globalRootPitchClass,
            (globalRootPitchClass + 5) % 12,
            (globalRootPitchClass + 7) % 12
        };

        var bestRoot = candidates[0];
        var bestScore = double.MinValue;
        foreach (var candidate in candidates)
        {
            var third = (candidate + (isMinor ? 3 : 4)) % 12;
            var fifth = (candidate + 7) % 12;
            var score = 0.0;
            foreach (var note in melodyInMeasure)
            {
                var pitchClass = PositiveModulo(note.MidiNumber, 12);
                if (pitchClass == candidate)
                {
                    score += 2.4;
                }
                else if (pitchClass == third)
                {
                    score += 1.8;
                }
                else if (pitchClass == fifth)
                {
                    score += 1.6;
                }
                else if (pitchClass == globalRootPitchClass)
                {
                    score += 0.8;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestRoot = candidate;
            }
        }

        return bestRoot;
    }

    private static KeyDetectionResult DetectKey(IReadOnlyList<PitchContourPoint> contour)
    {
        if (contour.Count < 8)
        {
            return new KeyDetectionResult("C", "major", 0, false, 0.0);
        }

        var pitchClassWeights = new double[12];
        foreach (var point in contour)
        {
            var midi = FrequencyToMidi(point.FrequencyHz);
            var pitchClass = PositiveModulo((int)Math.Round(midi), 12);
            pitchClassWeights[pitchClass] += 1.0;
        }

        var majorBest = GetBestProfileMatch(pitchClassWeights, MajorProfile);
        var minorBest = GetBestProfileMatch(pitchClassWeights, MinorProfile);
        var useMajor = majorBest.Score >= minorBest.Score;
        var scoreGap = Math.Abs(majorBest.Score - minorBest.Score);
        var bestScore = Math.Max(majorBest.Score, minorBest.Score);
        var confidence = bestScore <= 0
            ? 0.0
            : Math.Clamp(scoreGap / bestScore, 0.0, 1.0);
        return new KeyDetectionResult(
            SharpPitchNames[useMajor ? majorBest.Root : minorBest.Root],
            useMajor ? "major" : "minor",
            useMajor ? majorBest.Root : minorBest.Root,
            scoreGap > contour.Count * 0.08,
            confidence);
    }

    private static string FormatKeySignature(KeyDetectionResult detection)
    {
        return string.Equals(detection.Mode, "minor", StringComparison.OrdinalIgnoreCase)
            ? detection.Key + "m"
            : detection.Key;
    }

    private static (int Root, double Score) GetBestProfileMatch(IReadOnlyList<double> pitchClassWeights, IReadOnlyList<double> profile)
    {
        var bestRoot = 0;
        var bestScore = double.MinValue;
        for (var root = 0; root < 12; root++)
        {
            var score = 0.0;
            for (var index = 0; index < 12; index++)
            {
                score += pitchClassWeights[index] * profile[(index - root + 12) % 12];
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestRoot = root;
            }
        }

        return (bestRoot, bestScore);
    }

    private static GeneratedNoteResult CreateNote(
        int measureNo,
        double beatStart,
        DurationToken token,
        int midiNumber,
        string staff,
        double startTimeSeconds,
        bool isChordTone)
    {
        return new GeneratedNoteResult
        {
            MeasureNo = measureNo,
            BeatStart = Math.Round(beatStart, 3),
            DurationType = token.Type,
            DurationBeats = token.Beats,
            PitchName = MidiToPitchName(midiNumber),
            MidiNumber = midiNumber,
            Staff = staff,
            StartTimeSeconds = Math.Round(startTimeSeconds, 3),
            IsChordTone = isChordTone
        };
    }

    private static DurationToken NormalizeDuration(double beats)
    {
        if (beats <= 0.375)
        {
            return DurationToken.Sixteenth;
        }

        if (beats <= 0.75)
        {
            return DurationToken.Eighth;
        }

        if (beats <= 1.25)
        {
            return DurationToken.Quarter;
        }

        if (beats <= 1.75)
        {
            return DurationToken.DottedQuarter;
        }

        if (beats <= 2.5)
        {
            return DurationToken.Half;
        }

        if (beats <= 3.5)
        {
            return DurationToken.DottedHalf;
        }

        return DurationToken.Whole;
    }

    private static int FindClosestMidi(int pitchClass, int targetMidi)
    {
        var octaveBase = targetMidi / 12;
        var bestMidi = targetMidi;
        var bestDistance = int.MaxValue;
        for (var octave = octaveBase - 1; octave <= octaveBase + 1; octave++)
        {
            var midi = octave * 12 + pitchClass;
            var distance = Math.Abs(midi - targetMidi);
            if (distance < bestDistance)
            {
                bestMidi = midi;
                bestDistance = distance;
            }
        }

        return bestMidi;
    }

    private static string MidiToPitchName(int midiNumber)
    {
        var pitchClass = PositiveModulo(midiNumber, 12);
        var octave = midiNumber / 12 - 1;
        return SharpPitchNames[pitchClass] + octave.ToString(CultureInfo.InvariantCulture);
    }

    private static float[] Downsample(float[] samples, int originalSampleRate, out int sampleRate)
    {
        if (originalSampleRate <= TargetSampleRate)
        {
            sampleRate = originalSampleRate;
            return samples;
        }

        var factor = Math.Max(1, originalSampleRate / TargetSampleRate);
        sampleRate = originalSampleRate / factor;
        var frameCount = samples.Length / factor;
        var downsampled = new float[frameCount];

        for (var index = 0; index < frameCount; index++)
        {
            var sum = 0.0;
            for (var offset = 0; offset < factor; offset++)
            {
                sum += samples[index * factor + offset];
            }

            downsampled[index] = (float)(sum / factor);
        }

        return downsampled;
    }

    private static float[] NormalizeSamples(float[] samples)
    {
        if (samples.Length == 0)
        {
            return samples;
        }

        var peak = samples.Max(sample => Math.Abs(sample));
        if (peak <= 0)
        {
            return samples;
        }

        var normalized = new float[samples.Length];
        for (var index = 0; index < samples.Length; index++)
        {
            normalized[index] = samples[index] / peak;
        }

        return normalized;
    }

    private static double? DetectPitch(
        float[] samples,
        int frameStart,
        int frameSize,
        int sampleRate,
        double minimumEnergy,
        double minimumCorrelation)
    {
        var energy = 0.0;
        for (var index = 0; index < frameSize; index++)
        {
            var sample = samples[frameStart + index];
            energy += sample * sample;
        }

        var rms = Math.Sqrt(energy / frameSize);
        if (rms < minimumEnergy)
        {
            return null;
        }

        var minLag = Math.Max(1, (int)Math.Floor(sampleRate / MaximumPitchHz));
        var maxLag = Math.Min(frameSize / 2, (int)Math.Ceiling(sampleRate / MinimumPitchHz));
        var bestLag = 0;
        var bestScore = 0.0;

        for (var lag = minLag; lag <= maxLag; lag++)
        {
            var correlation = 0.0;
            var energyA = 0.0;
            var energyB = 0.0;

            for (var index = 0; index < frameSize - lag; index++)
            {
                var sampleA = samples[frameStart + index];
                var sampleB = samples[frameStart + index + lag];
                correlation += sampleA * sampleB;
                energyA += sampleA * sampleA;
                energyB += sampleB * sampleB;
            }

            if (energyA <= 0 || energyB <= 0)
            {
                continue;
            }

            var normalizedCorrelation = correlation / Math.Sqrt(energyA * energyB);
            if (normalizedCorrelation > bestScore)
            {
                bestScore = normalizedCorrelation;
                bestLag = lag;
            }
        }

        if (bestLag == 0 || bestScore < minimumCorrelation)
        {
            return null;
        }

        return sampleRate / (double)bestLag;
    }

    private static List<PitchContourPoint> SmoothPoints(IReadOnlyList<PitchContourPoint> points, int radius)
    {
        if (points.Count == 0 || radius <= 0)
        {
            return points.ToList();
        }

        var smoothed = new List<PitchContourPoint>(points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            var start = Math.Max(0, index - radius);
            var end = Math.Min(points.Count - 1, index + radius);
            var averageFrequency = points.Skip(start).Take(end - start + 1).Average(point => point.FrequencyHz);
            smoothed.Add(new PitchContourPoint(points[index].TimeSeconds, averageFrequency));
        }

        return smoothed;
    }

    private static double FrequencyToMidi(double frequencyHz)
    {
        return 69.0 + 12.0 * Math.Log2(frequencyHz / 440.0);
    }

    private static double Quantize(double value, double quantum)
    {
        return Math.Round(value / quantum) * quantum;
    }

    private static int PositiveModulo(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private sealed record PitchContourPoint(double TimeSeconds, double FrequencyHz);

    private sealed record KeyDetectionResult(string Key, string Mode, int RootPitchClass, bool IsReliable, double Confidence);

    private sealed record ResolvedRhythmGrid(
        double BeatDurationSeconds,
        int TimeSignatureNumerator,
        double TempoBpm,
        double Quantization,
        string GridSource,
        double TimeSignatureConfidence,
        string Summary);
}

internal static class MusicXmlBuilder
{
    private const int Divisions = 4;

    public static string Build(
        string title,
        int beats,
        int beatType,
        double tempoBpm,
        string keySignature,
        IReadOnlyList<GeneratedTrackResult> tracks)
    {
        var measureCount = tracks.SelectMany(track => track.Notes).DefaultIfEmpty().Max(note => note == null ? 1 : note.MeasureNo);
        var rightTrack = tracks.FirstOrDefault(track => string.Equals(track.HandRole, "right", StringComparison.OrdinalIgnoreCase));
        var leftTrack = tracks.FirstOrDefault(track => string.Equals(track.HandRole, "left", StringComparison.OrdinalIgnoreCase));
        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.AppendLine("<score-partwise version=\"3.1\">");
        builder.AppendLine($"  <work><work-title>{EscapeXml(title)}</work-title></work>");
        builder.AppendLine("  <part-list>");
        builder.AppendLine("    <score-part id=\"P1\">");
        builder.AppendLine("      <part-name>Piano</part-name>");
        builder.AppendLine("      <part-abbreviation>Pno.</part-abbreviation>");
        builder.AppendLine("    </score-part>");
        builder.AppendLine("  </part-list>");
        builder.AppendLine("  <part id=\"P1\">");
        for (var measureNo = 1; measureNo <= measureCount; measureNo++)
        {
            builder.AppendLine($"    <measure number=\"{measureNo}\">");
            if (measureNo == 1)
            {
                builder.AppendLine("      <attributes>");
                builder.AppendLine($"        <divisions>{Divisions}</divisions>");
                builder.AppendLine($"        <key><fifths>{KeySignatureToFifths(keySignature)}</fifths></key>");
                builder.AppendLine($"        <time><beats>{beats}</beats><beat-type>{beatType}</beat-type></time>");
                builder.AppendLine("        <staves>2</staves>");
                builder.AppendLine("        <clef number=\"1\"><sign>G</sign><line>2</line></clef>");
                builder.AppendLine("        <clef number=\"2\"><sign>F</sign><line>4</line></clef>");
                builder.AppendLine("      </attributes>");
                builder.AppendLine("      <direction placement=\"above\">");
                builder.AppendLine("        <direction-type><metronome><beat-unit>quarter</beat-unit>");
                builder.AppendLine($"          <per-minute>{tempoBpm.ToString("0.0", CultureInfo.InvariantCulture)}</per-minute></metronome></direction-type>");
                builder.AppendLine("        <sound tempo=\"" + tempoBpm.ToString("0.0", CultureInfo.InvariantCulture) + "\"/>");
                builder.AppendLine("      </direction>");
            }

            RenderTrackMeasure(
                builder,
                rightTrack?.Notes.Where(note => note.MeasureNo == measureNo).ToList(),
                beats,
                voiceNumber: 1,
                staffNumber: 1);

            builder.AppendLine("      <backup>");
            builder.AppendLine($"        <duration>{beats * Divisions}</duration>");
            builder.AppendLine("      </backup>");

            RenderTrackMeasure(
                builder,
                leftTrack?.Notes.Where(note => note.MeasureNo == measureNo).ToList(),
                beats,
                voiceNumber: 2,
                staffNumber: 2);

            builder.AppendLine("    </measure>");
        }

        builder.AppendLine("  </part>");
        builder.AppendLine("</score-partwise>");
        return builder.ToString();
    }

    private static void RenderTrackMeasure(
        StringBuilder builder,
        IReadOnlyList<GeneratedNoteResult>? notes,
        int beats,
        int voiceNumber,
        int staffNumber)
    {
        var orderedNotes = (notes ?? Array.Empty<GeneratedNoteResult>())
            .OrderBy(note => note.BeatStart)
            .ThenBy(note => note.MidiNumber)
            .ToList();

        var cursor = 0.0;
        foreach (var note in orderedNotes)
        {
            if (note.BeatStart > cursor + 0.001)
            {
                foreach (var rest in SplitDuration(note.BeatStart - cursor))
                {
                    AppendRest(builder, rest, voiceNumber, staffNumber);
                }
            }

            AppendNote(builder, note, voiceNumber, staffNumber);
            cursor = Math.Max(cursor, note.BeatStart + note.DurationBeats);
        }

        if (cursor < beats - 0.001)
        {
            foreach (var rest in SplitDuration(beats - cursor))
            {
                AppendRest(builder, rest, voiceNumber, staffNumber);
            }
        }
    }

    private static void AppendNote(StringBuilder builder, GeneratedNoteResult note, int voiceNumber, int staffNumber)
    {
        builder.AppendLine("      <note>");
        AppendPitch(builder, note.PitchName);
        builder.AppendLine($"        <duration>{(int)Math.Round(note.DurationBeats * Divisions)}</duration>");
        builder.AppendLine($"        <type>{MapDurationType(note.DurationType)}</type>");
        if (note.DurationType.StartsWith("dotted", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("        <dot/>");
        }

        builder.AppendLine($"        <voice>{voiceNumber}</voice>");
        builder.AppendLine($"        <staff>{staffNumber}</staff>");
        builder.AppendLine("      </note>");
    }

    private static void AppendRest(StringBuilder builder, DurationToken token, int voiceNumber, int staffNumber)
    {
        builder.AppendLine("      <note>");
        builder.AppendLine("        <rest/>");
        builder.AppendLine($"        <duration>{(int)Math.Round(token.Beats * Divisions)}</duration>");
        builder.AppendLine($"        <type>{MapDurationType(token.Type)}</type>");
        if (token.Type.StartsWith("dotted", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("        <dot/>");
        }

        builder.AppendLine($"        <voice>{voiceNumber}</voice>");
        builder.AppendLine($"        <staff>{staffNumber}</staff>");
        builder.AppendLine("      </note>");
    }

    private static void AppendPitch(StringBuilder builder, string pitchName)
    {
        ParsePitchName(pitchName, out var step, out var alter, out var octave);
        builder.AppendLine("        <pitch>");
        builder.AppendLine($"          <step>{step}</step>");
        if (alter != 0)
        {
            builder.AppendLine($"          <alter>{alter}</alter>");
        }

        builder.AppendLine($"          <octave>{octave}</octave>");
        builder.AppendLine("        </pitch>");
    }

    private static IEnumerable<DurationToken> SplitDuration(double beats)
    {
        var remaining = beats;
        while (remaining > 0.001)
        {
            if (remaining >= DurationToken.Whole.Beats - 0.001)
            {
                yield return DurationToken.Whole;
                remaining -= DurationToken.Whole.Beats;
                continue;
            }

            if (remaining >= DurationToken.DottedHalf.Beats - 0.001)
            {
                yield return DurationToken.DottedHalf;
                remaining -= DurationToken.DottedHalf.Beats;
                continue;
            }

            if (remaining >= DurationToken.Half.Beats - 0.001)
            {
                yield return DurationToken.Half;
                remaining -= DurationToken.Half.Beats;
                continue;
            }

            if (remaining >= DurationToken.DottedQuarter.Beats - 0.001)
            {
                yield return DurationToken.DottedQuarter;
                remaining -= DurationToken.DottedQuarter.Beats;
                continue;
            }

            if (remaining >= DurationToken.Quarter.Beats - 0.001)
            {
                yield return DurationToken.Quarter;
                remaining -= DurationToken.Quarter.Beats;
                continue;
            }

            if (remaining >= DurationToken.Eighth.Beats - 0.001)
            {
                yield return DurationToken.Eighth;
                remaining -= DurationToken.Eighth.Beats;
                continue;
            }

            yield return DurationToken.Sixteenth;
            remaining -= DurationToken.Sixteenth.Beats;
        }
    }

    private static string MapDurationType(string durationType)
    {
        return durationType switch
        {
            "whole" => "whole",
            "dotted-half" => "half",
            "half" => "half",
            "dotted-quarter" => "quarter",
            "quarter" => "quarter",
            "eighth" => "eighth",
            "sixteenth" => "16th",
            _ => "quarter"
        };
    }

    private static int KeySignatureToFifths(string keySignature)
    {
        return keySignature switch
        {
            "C" => 0,
            "G" => 1,
            "D" => 2,
            "A" => 3,
            "E" => 4,
            "B" => 5,
            "F#" => 6,
            "C#" => 7,
            "F" => -1,
            "Bb" => -2,
            "Eb" => -3,
            "Ab" => -4,
            "Db" => -5,
            "Gb" => -6,
            "Cb" => -7,
            "Am" => 0,
            "Em" => 1,
            "Bm" => 2,
            "F#m" => 3,
            "C#m" => 4,
            "G#m" => 5,
            "D#m" => 6,
            "A#m" => 7,
            "Dm" => -1,
            "Gm" => -2,
            "Cm" => -3,
            "Fm" => -4,
            "Bbm" => -5,
            "Ebm" => -6,
            "Abm" => -7,
            _ => 0
        };
    }

    private static void ParsePitchName(string pitchName, out string step, out int alter, out int octave)
    {
        step = pitchName.Substring(0, 1);
        alter = pitchName.Contains('#', StringComparison.Ordinal) ? 1 : pitchName.Contains('b', StringComparison.Ordinal) ? -1 : 0;
        var octaveText = new string(pitchName.Where(char.IsDigit).ToArray());
        octave = int.TryParse(octaveText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 4;
    }

    private static string EscapeXml(string value)
    {
        return System.Security.SecurityElement.Escape(value) ?? string.Empty;
    }
}

internal static class ScorePreviewRenderer
{
    private const double PageWidth = 1024;
    private const double PageHeight = 1320;
    private const double SystemLeft = 110;
    private const double SystemTop = 160;
    private const double SystemWidth = 820;
    private const double SystemGap = 420;
    private const double TrebleBottomY = 86;
    private const double BassBottomY = 176;
    private const double StaffStep = 6;

    public static int CalculatePageCount(int measureCount, int measuresPerSystem, int systemsPerPage)
    {
        var measuresPerPage = Math.Max(1, measuresPerSystem * systemsPerPage);
        return Math.Max(1, (int)Math.Ceiling(measureCount / (double)measuresPerPage));
    }

    public static List<ScorePreviewPageResponse> RenderPages(
        string title,
        double? tempoBpm,
        string timeSignature,
        string keySignature,
        int measureCount,
        int measuresPerSystem,
        int systemsPerPage,
        IReadOnlyList<PreviewTrack> tracks)
    {
        var pageCount = CalculatePageCount(measureCount, measuresPerSystem, systemsPerPage);
        var pages = new List<ScorePreviewPageResponse>(pageCount);
        var measuresPerPage = Math.Max(1, measuresPerSystem * systemsPerPage);

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var pageStartMeasure = pageIndex * measuresPerPage + 1;
            var pageEndMeasure = Math.Min(measureCount, pageStartMeasure + measuresPerPage - 1);
            var builder = new StringBuilder();
            builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{PageWidth}\" height=\"{PageHeight}\" viewBox=\"0 0 {PageWidth} {PageHeight}\">");
            builder.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

            if (pageIndex == 0)
            {
                builder.AppendLine($"<text x=\"{PageWidth / 2}\" y=\"70\" text-anchor=\"middle\" font-size=\"34\" font-family=\"Georgia, serif\" fill=\"#1F2A44\">{EscapeXml(title)}</text>");
                builder.AppendLine($"<text x=\"{PageWidth / 2}\" y=\"106\" text-anchor=\"middle\" font-size=\"15\" font-family=\"Segoe UI, sans-serif\" fill=\"#60708C\">钢琴双手谱预览</text>");
            }

            var firstPageLabelY = pageIndex == 0 ? 132 : 86;
            builder.AppendLine($"<text x=\"70\" y=\"{firstPageLabelY}\" font-size=\"15\" font-family=\"Segoe UI, sans-serif\" fill=\"#60708C\">第 {pageIndex + 1} / {pageCount} 页</text>");

            for (var systemIndex = 0; systemIndex < systemsPerPage; systemIndex++)
            {
                var firstMeasure = pageStartMeasure + systemIndex * measuresPerSystem;
                if (firstMeasure > pageEndMeasure)
                {
                    break;
                }

                var lastMeasure = Math.Min(pageEndMeasure, firstMeasure + measuresPerSystem - 1);
                var measureCountInSystem = lastMeasure - firstMeasure + 1;
                var systemY = SystemTop + systemIndex * SystemGap - (pageIndex == 0 ? 0 : 36);

                DrawGrandStaff(builder, systemY, firstMeasure == 1, timeSignature, keySignature, tempoBpm, measureCountInSystem);
                DrawMeasures(builder, systemY, firstMeasure, lastMeasure, measureCountInSystem);
                DrawNotes(builder, systemY, firstMeasure, lastMeasure, measureCountInSystem, tracks);
            }

            builder.AppendLine("</svg>");
            pages.Add(new ScorePreviewPageResponse
            {
                PageNumber = pageIndex + 1,
                SvgContent = builder.ToString()
            });
        }

        return pages;
    }

    private static void DrawGrandStaff(
        StringBuilder builder,
        double systemY,
        bool isFirstSystem,
        string timeSignature,
        string keySignature,
        double? tempoBpm,
        int measureCountInSystem)
    {
        var trebleBase = systemY + TrebleBottomY;
        var bassBase = systemY + BassBottomY;
        builder.AppendLine($"<text x=\"40\" y=\"{systemY + 118}\" font-size=\"30\" font-family=\"Georgia, serif\" fill=\"#1F2A44\">Piano</text>");
        builder.AppendLine($"<line x1=\"88\" y1=\"{systemY + 46}\" x2=\"88\" y2=\"{systemY + 190}\" stroke=\"#1F2A44\" stroke-width=\"2\"/>");

        for (var index = 0; index < 5; index++)
        {
            var yTreble = trebleBase - index * StaffStep * 2;
            var yBass = bassBase - index * StaffStep * 2;
            builder.AppendLine($"<line x1=\"{SystemLeft}\" y1=\"{yTreble}\" x2=\"{SystemLeft + SystemWidth}\" y2=\"{yTreble}\" stroke=\"#2B2F38\" stroke-width=\"1\"/>");
            builder.AppendLine($"<line x1=\"{SystemLeft}\" y1=\"{yBass}\" x2=\"{SystemLeft + SystemWidth}\" y2=\"{yBass}\" stroke=\"#2B2F38\" stroke-width=\"1\"/>");
        }

        builder.AppendLine($"<text x=\"{SystemLeft - 28}\" y=\"{systemY + 70}\" font-size=\"54\" font-family=\"Times New Roman, serif\" fill=\"#111\">&#119070;</text>");
        builder.AppendLine($"<text x=\"{SystemLeft - 20}\" y=\"{systemY + 160}\" font-size=\"44\" font-family=\"Times New Roman, serif\" fill=\"#111\">&#119074;</text>");
        builder.AppendLine($"<text x=\"{SystemLeft + 28}\" y=\"{systemY + 76}\" font-size=\"24\" font-family=\"Georgia, serif\" fill=\"#111\">{EscapeXml(keySignature)}</text>");
        builder.AppendLine($"<text x=\"{SystemLeft + 30}\" y=\"{systemY + 156}\" font-size=\"24\" font-family=\"Georgia, serif\" fill=\"#111\">{EscapeXml(timeSignature)}</text>");

        if (isFirstSystem && tempoBpm.HasValue)
        {
            builder.AppendLine($"<text x=\"{SystemLeft + 134}\" y=\"{systemY + 28}\" font-size=\"18\" font-family=\"Georgia, serif\" fill=\"#111\">Quarter = {tempoBpm.Value:0}</text>");
        }

        var systemRight = SystemLeft + SystemWidth;
        builder.AppendLine($"<line x1=\"{systemRight}\" y1=\"{systemY + 38}\" x2=\"{systemRight}\" y2=\"{systemY + 190}\" stroke=\"#1F2A44\" stroke-width=\"2.4\"/>");
        builder.AppendLine($"<text x=\"{SystemLeft + SystemWidth - 6}\" y=\"{systemY + 220}\" text-anchor=\"end\" font-size=\"13\" font-family=\"Segoe UI, sans-serif\" fill=\"#6E7F9B\">{measureCountInSystem} 小节 / 系统</text>");
    }

    private static void DrawMeasures(
        StringBuilder builder,
        double systemY,
        int firstMeasure,
        int lastMeasure,
        int measureCountInSystem)
    {
        var measureWidth = SystemWidth / measureCountInSystem;
        for (var offset = 0; offset < measureCountInSystem; offset++)
        {
            var x = SystemLeft + offset * measureWidth;
            builder.AppendLine($"<line x1=\"{x}\" y1=\"{systemY + 38}\" x2=\"{x}\" y2=\"{systemY + 190}\" stroke=\"#4A5568\" stroke-width=\"1\"/>");
            builder.AppendLine($"<text x=\"{x + 6}\" y=\"{systemY + 20}\" font-size=\"13\" font-family=\"Segoe UI, sans-serif\" fill=\"#72809A\">{firstMeasure + offset}</text>");
        }

        var right = SystemLeft + measureCountInSystem * measureWidth;
        builder.AppendLine($"<line x1=\"{right}\" y1=\"{systemY + 38}\" x2=\"{right}\" y2=\"{systemY + 190}\" stroke=\"#4A5568\" stroke-width=\"1\"/>");
    }

    private static void DrawNotes(
        StringBuilder builder,
        double systemY,
        int firstMeasure,
        int lastMeasure,
        int measureCountInSystem,
        IReadOnlyList<PreviewTrack> tracks)
    {
        var measureWidth = SystemWidth / measureCountInSystem;
        var rightTrack = tracks.FirstOrDefault(track => string.Equals(track.HandRole, "right", StringComparison.OrdinalIgnoreCase));
        var leftTrack = tracks.FirstOrDefault(track => string.Equals(track.HandRole, "left", StringComparison.OrdinalIgnoreCase));
        if (rightTrack != null)
        {
            foreach (var note in rightTrack.Notes.Where(note => note.MeasureNo >= firstMeasure && note.MeasureNo <= lastMeasure))
            {
                DrawNote(builder, systemY, firstMeasure, measureWidth, note, "treble");
            }
        }

        if (leftTrack != null)
        {
            foreach (var note in leftTrack.Notes.Where(note => note.MeasureNo >= firstMeasure && note.MeasureNo <= lastMeasure))
            {
                DrawNote(builder, systemY, firstMeasure, measureWidth, note, "bass");
            }
        }
    }

    private static void DrawNote(
        StringBuilder builder,
        double systemY,
        int firstMeasure,
        double measureWidth,
        PreviewNote note,
        string staff)
    {
        const double noteHeadWidth = 12;
        const double noteHeadHeight = 8;
        var x = SystemLeft + (note.MeasureNo - firstMeasure) * measureWidth + 52 + (measureWidth - 78) * (note.BeatStart / 4.0);
        var y = GetNoteY(systemY, note.PitchName, staff);
        var fill = note.DurationType is "half" or "whole" or "dotted-half" ? "#ffffff" : "#111111";
        builder.AppendLine($"<ellipse cx=\"{x:0.##}\" cy=\"{y:0.##}\" rx=\"{noteHeadWidth / 2}\" ry=\"{noteHeadHeight / 2}\" fill=\"{fill}\" stroke=\"#111111\" stroke-width=\"1.4\" transform=\"rotate(-18 {x:0.##} {y:0.##})\"/>");

        var stemUp = ShouldStemUp(note.PitchName, staff);
        if (!string.Equals(note.DurationType, "whole", StringComparison.OrdinalIgnoreCase))
        {
            var stemX = stemUp ? x + noteHeadWidth / 2 - 1 : x - noteHeadWidth / 2 + 1;
            var stemEndY = stemUp ? y - 34 : y + 34;
            builder.AppendLine($"<line x1=\"{stemX:0.##}\" y1=\"{y:0.##}\" x2=\"{stemX:0.##}\" y2=\"{stemEndY:0.##}\" stroke=\"#111111\" stroke-width=\"1.3\"/>");

            var flagCount = note.DurationType == "sixteenth" ? 2 : note.DurationType == "eighth" ? 1 : 0;
            for (var flagIndex = 0; flagIndex < flagCount; flagIndex++)
            {
                var flagStartY = stemUp ? stemEndY + flagIndex * 8 : stemEndY - flagIndex * 8;
                var controlY = stemUp ? flagStartY + 6 : flagStartY - 6;
                var endY = stemUp ? flagStartY + 14 : flagStartY - 14;
                var endX = stemUp ? stemX + 10 : stemX - 10;
                builder.AppendLine($"<path d=\"M {stemX:0.##},{flagStartY:0.##} Q {(stemUp ? stemX + 9 : stemX - 9):0.##},{controlY:0.##} {endX:0.##},{endY:0.##}\" fill=\"none\" stroke=\"#111111\" stroke-width=\"1.2\"/>");
            }
        }

        if (note.DurationType.StartsWith("dotted", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"<circle cx=\"{x + 10:0.##}\" cy=\"{y - 1:0.##}\" r=\"1.8\" fill=\"#111111\"/>");
        }

        var accidental = GetAccidental(note.PitchName);
        if (!string.IsNullOrWhiteSpace(accidental))
        {
            builder.AppendLine($"<text x=\"{x - 16:0.##}\" y=\"{y + 4:0.##}\" font-size=\"16\" font-family=\"Georgia, serif\" fill=\"#111111\">{EscapeXml(accidental)}</text>");
        }

        foreach (var ledgerY in GetLedgerLines(systemY, note.PitchName, staff))
        {
            builder.AppendLine($"<line x1=\"{x - 10:0.##}\" y1=\"{ledgerY:0.##}\" x2=\"{x + 10:0.##}\" y2=\"{ledgerY:0.##}\" stroke=\"#111111\" stroke-width=\"1\"/>");
        }
    }

    private static IEnumerable<double> GetLedgerLines(double systemY, string pitchName, string staff)
    {
        var baseY = staff == "bass" ? systemY + BassBottomY : systemY + TrebleBottomY;
        var baseIndex = staff == "bass" ? ToDiatonicIndex("G2") : ToDiatonicIndex("E4");
        var noteIndex = ToDiatonicIndex(pitchName);
        var position = noteIndex - baseIndex;
        if (position < 0)
        {
            for (var value = position; value <= -1; value++)
            {
                if ((value & 1) == 0)
                {
                    yield return baseY - value * StaffStep;
                }
            }
        }
        else if (position > 8)
        {
            for (var value = 10; value <= position; value++)
            {
                if ((value & 1) == 0)
                {
                    yield return baseY - value * StaffStep;
                }
            }
        }
    }

    private static bool ShouldStemUp(string pitchName, string staff)
    {
        var index = ToDiatonicIndex(pitchName);
        var middle = staff == "bass" ? ToDiatonicIndex("D3") : ToDiatonicIndex("B4");
        return index <= middle;
    }

    private static double GetNoteY(double systemY, string pitchName, string staff)
    {
        var baseY = staff == "bass" ? systemY + BassBottomY : systemY + TrebleBottomY;
        var baseIndex = staff == "bass" ? ToDiatonicIndex("G2") : ToDiatonicIndex("E4");
        var noteIndex = ToDiatonicIndex(pitchName);
        return baseY - (noteIndex - baseIndex) * StaffStep;
    }

    private static int ToDiatonicIndex(string pitchName)
    {
        var step = pitchName[0] switch
        {
            'C' => 0,
            'D' => 1,
            'E' => 2,
            'F' => 3,
            'G' => 4,
            'A' => 5,
            'B' => 6,
            _ => 0
        };

        var octaveText = new string(pitchName.Where(char.IsDigit).ToArray());
        var octave = int.TryParse(octaveText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 4;
        return octave * 7 + step;
    }

    private static string? GetAccidental(string pitchName)
    {
        if (pitchName.Contains('#', StringComparison.Ordinal))
        {
            return "#";
        }

        if (pitchName.Contains('b', StringComparison.Ordinal))
        {
            return "b";
        }

        return null;
    }

    private static string EscapeXml(string value)
    {
        return System.Security.SecurityElement.Escape(value) ?? string.Empty;
    }
}

internal sealed class PreviewTrack
{
    public string HandRole { get; set; } = string.Empty;
    public List<PreviewNote> Notes { get; set; } = new();
}

internal sealed class PreviewNote
{
    public int MeasureNo { get; set; }
    public double BeatStart { get; set; }
    public string DurationType { get; set; } = string.Empty;
    public string PitchName { get; set; } = string.Empty;
}

internal sealed record DurationToken(string Type, double Beats)
{
    public static readonly DurationToken Sixteenth = new("sixteenth", 0.25);
    public static readonly DurationToken Eighth = new("eighth", 0.5);
    public static readonly DurationToken Quarter = new("quarter", 1.0);
    public static readonly DurationToken DottedQuarter = new("dotted-quarter", 1.5);
    public static readonly DurationToken Half = new("half", 2.0);
    public static readonly DurationToken DottedHalf = new("dotted-half", 3.0);
    public static readonly DurationToken Whole = new("whole", 4.0);
}
