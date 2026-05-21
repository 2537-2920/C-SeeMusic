using backend.Models;

namespace backend.Services;

public sealed class PitchAnalysisService : IPitchAnalysisService
{
    private const int TargetSampleRate = 11025;
    private const double MinimumPitchHz = 80.0;
    private const double MaximumPitchHz = 1000.0;
    private static readonly string[] PitchClassNames =
    {
        "C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"
    };

    private static readonly double[] MajorProfile =
    {
        6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88
    };

    private static readonly double[] MinorProfile =
    {
        6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17
    };

    public PitchAnalysisResult Analyze(string performancePath, string? referencePath, EvaluationOptionsRequest options)
    {
        var normalizedOptions = (options ?? new EvaluationOptionsRequest()).Normalize();
        var isEnglish = IsEnglish(normalizedOptions);

        if (string.IsNullOrWhiteSpace(referencePath))
        {
            return new PitchAnalysisResult
            {
                Status = "failed",
                Summary = isEnglish
                    ? "No reference audio was provided, so the comparison pitch evaluation could not continue."
                    : "未上传标准音频，无法继续进行音准对比评估。",
            };
        }

        if (!File.Exists(performancePath))
        {
            return new PitchAnalysisResult
            {
                Status = "failed",
                Summary = isEnglish
                    ? "The performance audio file is missing, so pitch analysis could not run."
                    : "演唱音频不存在，无法分析音准。",
            };
        }

        if (!File.Exists(referencePath))
        {
            return new PitchAnalysisResult
            {
                Status = "failed",
                Summary = isEnglish
                    ? "The reference audio file is missing, so the comparison pitch evaluation could not continue."
                    : "标准音频不存在，无法继续进行音准对比评估。",
            };
        }

        try
        {
            var performanceAudio = WavAudioReader.Read(performancePath);
            var referenceAudio = WavAudioReader.Read(referencePath);

            var performanceCurve = ExtractPitchCurve(performanceAudio, normalizedOptions);
            if (performanceCurve.Count < 8)
            {
                return new PitchAnalysisResult
                {
                    Status = "failed",
                    Summary = isEnglish
                        ? "No stable pitch contour could be extracted from the performance audio."
                        : "演唱音频未提取到稳定音高，无法输出音准评分。",
                };
            }

            var referenceCurve = ExtractPitchCurve(referenceAudio, normalizedOptions);
            var referenceVoicedCoverage = EstimateVoicedCoverage(referenceCurve, referenceAudio.DurationSeconds);
            if (referenceCurve.Count < 8 || referenceVoicedCoverage < 0.25)
            {
                return new PitchAnalysisResult
                {
                    Status = "failed",
                    Summary = isEnglish
                        ? "The reference audio did not contain enough stable melody to complete the comparison pitch evaluation."
                        : "标准音频未提取到足够稳定的主旋律，无法继续进行音准对比评估。",
                };
            }

            var matches = AlignCurves(
                performanceCurve,
                referenceCurve,
                performanceAudio.DurationSeconds,
                referenceAudio.DurationSeconds);

            if (matches.Count < 8)
            {
                return new PitchAnalysisResult
                {
                    Status = "failed",
                    Summary = isEnglish
                        ? "The aligned pitch fragments were too sparse to complete a reliable comparison pitch evaluation."
                        : "标准音频与演唱音频的可对齐音高片段不足，无法继续进行音准对比评估。",
                };
            }

            var absoluteDeviations = matches.Select(match => Math.Abs(match.DeviationCents)).ToList();
            var meanDeviationCents = absoluteDeviations.Average();
            var deviationStdDev = CalculateStandardDeviation(absoluteDeviations);
            var hitRate25 = matches.Count(match => Math.Abs(match.DeviationCents) <= 25.0) / (double)matches.Count * 100.0;
            var hitRate50 = matches.Count(match => Math.Abs(match.DeviationCents) <= 50.0) / (double)matches.Count * 100.0;
            var comparisonSampleCount = CalculateComparisonSampleCount(performanceCurve.Count, referenceCurve.Count);
            var coverage = matches.Count / (double)Math.Max(1, comparisonSampleCount) * 100.0;
            var consistency = Math.Clamp(100.0 - deviationStdDev / 1.6, 0.0, 100.0);
            var meanAccuracy = Math.Clamp(100.0 - meanDeviationCents / 1.2, 0.0, 100.0);
            var score = Math.Clamp(
                meanAccuracy * 0.70
                + hitRate25 * 0.15
                + hitRate50 * 0.10
                + consistency * 0.05,
                0.0,
                100.0);

            var keyDetection = DetectKey(referenceCurve);
            var result = new PitchAnalysisResult
            {
                Status = "succeeded",
                Score = Math.Round(score, 1),
                MeanDeviationCents = Math.Round(meanDeviationCents, 1),
                HitRate25 = Math.Round(hitRate25, 1),
                HitRate50 = Math.Round(hitRate50, 1),
                Coverage = Math.Round(coverage, 1),
                Consistency = Math.Round(consistency, 1),
                DetectedKey = keyDetection.Key,
                DetectedMode = keyDetection.Mode,
                ReferenceMedianMidi = keyDetection.ReferenceMedianMidi,
                Summary = isEnglish
                    ? $"Average pitch deviation is about {meanDeviationCents:F1} cents, with {coverage:F0}% aligned coverage."
                    : $"音准平均偏差约 {meanDeviationCents:F1} 音分，对齐覆盖率 {coverage:F0}%。",
                Segments = BuildSegments(matches, isEnglish),
                ReferencePoints = BuildPitchPointSeries(referenceCurve),
                PerformancePoints = BuildPitchPointSeries(performanceCurve),
                DeviationPoints = BuildDeviationSeries(matches),
            };

            if (coverage < 45.0)
            {
                result.Warnings.Add(isEnglish
                    ? "Pitch alignment coverage is limited, so treat the pitch result as a practice reference."
                    : "参考对齐覆盖率偏低，音准结果更适合作为练习参考。");
            }

            return result;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return new PitchAnalysisResult
            {
                Status = "failed",
                Summary = exception.Message,
            };
        }
    }

    private static List<PitchPoint> ExtractPitchCurve(
        WavAudioData audioData,
        EvaluationOptionsRequest options)
    {
        var downsampled = Downsample(audioData.Samples, audioData.SampleRate, out var sampleRate);
        var normalized = NormalizeSamples(downsampled);
        const int frameSize = 1024;
        const int hopSize = 256;

        var isCleanVocal = string.Equals(options.UserAudioType, "clean_vocal", StringComparison.OrdinalIgnoreCase);
        var minimumEnergy = isCleanVocal ? 0.014 : 0.018;
        var minimumCorrelation = isCleanVocal ? 0.50 : 0.52;
        var smoothingWindow = isCleanVocal ? 2 : 3;

        var points = new List<PitchPoint>();
        for (var frameStart = 0; frameStart + frameSize < normalized.Length; frameStart += hopSize)
        {
            var pitch = DetectPitch(normalized, frameStart, frameSize, sampleRate, minimumEnergy, minimumCorrelation);
            if (pitch == null)
            {
                continue;
            }

            var time = (frameStart + frameSize / 2.0) / sampleRate;
            points.Add(new PitchPoint(time, pitch.Value));
        }

        return SmoothPoints(points, smoothingWindow);
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

    private static List<PitchPoint> SmoothPoints(IReadOnlyList<PitchPoint> points, int radius)
    {
        if (points.Count == 0 || radius <= 0)
        {
            return points.ToList();
        }

        var smoothed = new List<PitchPoint>(points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            var start = Math.Max(0, index - radius);
            var end = Math.Min(points.Count - 1, index + radius);
            var averageFrequency = points.Skip(start).Take(end - start + 1).Average(point => point.FrequencyHz);
            smoothed.Add(new PitchPoint(points[index].TimeSeconds, averageFrequency));
        }

        return smoothed;
    }

    private static List<PitchMatch> AlignCurves(
        IReadOnlyList<PitchPoint> performanceCurve,
        IReadOnlyList<PitchPoint> referenceCurve,
        double performanceDurationSeconds,
        double referenceDurationSeconds)
    {
        var matches = new List<PitchMatch>();
        var sampleCount = CalculateComparisonSampleCount(performanceCurve.Count, referenceCurve.Count);
        for (var index = 0; index < sampleCount; index++)
        {
            var ratio = sampleCount == 1 ? 0.0 : index / (double)(sampleCount - 1);
            var performanceTargetTime = ratio * performanceDurationSeconds;
            var referenceTargetTime = ratio * referenceDurationSeconds;
            var performancePoint = FindNearest(performanceCurve, performanceTargetTime);
            var referencePoint = FindNearest(referenceCurve, referenceTargetTime);

            if (performancePoint == null
                || referencePoint == null
                || Math.Abs(performancePoint.TimeSeconds - performanceTargetTime) > 0.22
                || Math.Abs(referencePoint.TimeSeconds - referenceTargetTime) > 0.22)
            {
                continue;
            }

            var deviation = 1200.0 * Math.Log2(performancePoint.FrequencyHz / referencePoint.FrequencyHz);
            deviation = WrapDeviationToNearestOctave(deviation);
            matches.Add(new PitchMatch(performancePoint.TimeSeconds, performancePoint.FrequencyHz, referencePoint.FrequencyHz, deviation));
        }

        return matches;
    }

    private static int CalculateComparisonSampleCount(int performanceCount, int referenceCount)
    {
        var minimum = Math.Min(performanceCount, referenceCount);
        return Math.Max(24, Math.Min(96, minimum));
    }

    private static double WrapDeviationToNearestOctave(double deviationCents)
    {
        while (deviationCents > 600.0)
        {
            deviationCents -= 1200.0;
        }

        while (deviationCents < -600.0)
        {
            deviationCents += 1200.0;
        }

        return deviationCents;
    }

    private static PitchPoint? FindNearest(IReadOnlyList<PitchPoint> points, double targetTime)
    {
        if (points.Count == 0)
        {
            return null;
        }

        var low = 0;
        var high = points.Count - 1;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (points[middle].TimeSeconds < targetTime)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        var candidate = points[low];
        if (low == 0)
        {
            return candidate;
        }

        var previous = points[low - 1];
        return Math.Abs(candidate.TimeSeconds - targetTime) < Math.Abs(previous.TimeSeconds - targetTime)
            ? candidate
            : previous;
    }

    private static KeyDetectionResult DetectKey(IReadOnlyList<PitchPoint> referenceCurve)
    {
        if (referenceCurve.Count < 8)
        {
            return new KeyDetectionResult("--", "--", null);
        }

        var pitchClassWeights = new double[12];
        var midiValues = new List<double>(referenceCurve.Count);

        foreach (var point in referenceCurve)
        {
            var midi = FrequencyToMidi(point.FrequencyHz);
            midiValues.Add(midi);
            var pitchClass = ((int)Math.Round(midi)) % 12;
            if (pitchClass < 0)
            {
                pitchClass += 12;
            }

            pitchClassWeights[pitchClass] += 1.0;
        }

        var majorBest = GetBestProfileMatch(pitchClassWeights, MajorProfile);
        var minorBest = GetBestProfileMatch(pitchClassWeights, MinorProfile);
        var useMajor = majorBest.Score >= minorBest.Score;
        return new KeyDetectionResult(
            PitchClassNames[useMajor ? majorBest.Root : minorBest.Root],
            useMajor ? "major" : "minor",
            Median(midiValues));
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

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var ordered = values.OrderBy(value => value).ToArray();
        var middle = ordered.Length / 2;
        if ((ordered.Length & 1) == 1)
        {
            return ordered[middle];
        }

        return (ordered[middle - 1] + ordered[middle]) / 2.0;
    }

    private static double FrequencyToMidi(double frequencyHz)
    {
        return 69.0 + 12.0 * Math.Log2(frequencyHz / 440.0);
    }

    private static double CalculateStandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count <= 1)
        {
            return 0.0;
        }

        var mean = values.Average();
        var variance = values.Sum(value => Math.Pow(value - mean, 2.0)) / values.Count;
        return Math.Sqrt(variance);
    }

    private static double EstimateVoicedCoverage(IReadOnlyList<PitchPoint> points, double durationSeconds)
    {
        if (points.Count == 0 || durationSeconds <= 0)
        {
            return 0.0;
        }

        const double hopSeconds = 256.0 / TargetSampleRate;
        return Math.Min(1.0, points.Count * hopSeconds / durationSeconds);
    }

    private static List<EvaluationSegmentDto> BuildSegments(IReadOnlyList<PitchMatch> matches, bool isEnglish)
    {
        var segments = new List<EvaluationSegmentDto>();
        if (matches.Count == 0)
        {
            return segments;
        }

        var current = new List<PitchMatch> { matches[0] };
        for (var index = 1; index < matches.Count; index++)
        {
            var previous = matches[index - 1];
            var next = matches[index];
            if (GetSeverity(previous.DeviationCents) != GetSeverity(next.DeviationCents)
                || next.TimeSeconds - previous.TimeSeconds > 0.6)
            {
                segments.Add(CreateSegment(current, isEnglish));
                current = new List<PitchMatch>();
            }

            current.Add(next);
        }

        if (current.Count > 0)
        {
            segments.Add(CreateSegment(current, isEnglish));
        }

        return segments.Take(12).ToList();
    }

    private static EvaluationSegmentDto CreateSegment(IReadOnlyList<PitchMatch> matches, bool isEnglish)
    {
        var averageDeviation = matches.Average(match => Math.Abs(match.DeviationCents));
        return new EvaluationSegmentDto
        {
            MetricType = "pitch",
            StartMs = (int)Math.Round(matches.First().TimeSeconds * 1000),
            EndMs = (int)Math.Round(matches.Last().TimeSeconds * 1000),
            Score = Math.Round(Math.Clamp(100.0 - averageDeviation / 1.2, 0.0, 100.0), 1),
            DeviationValue = Math.Round(averageDeviation, 1),
            DeviationUnit = "cents",
            Severity = GetSeverity(averageDeviation),
            NoteText = BuildSegmentNote(averageDeviation, isEnglish)
        };
    }

    private static string BuildSegmentNote(double averageDeviation, bool isEnglish)
    {
        if (averageDeviation <= 25)
        {
            return isEnglish
                ? "This phrase stays close to the reference melody."
                : "音高基本贴合参考旋律。";
        }

        if (averageDeviation <= 50)
        {
            return isEnglish
                ? "This phrase has a noticeable pitch offset."
                : "此段存在可感知音高偏差。";
        }

        return isEnglish
            ? "This phrase drifts far from the target pitch and should be practiced separately."
            : "此段音高偏差较大，建议拆句练习。";
    }

    private static string GetSeverity(double deviationCents)
    {
        var absolute = Math.Abs(deviationCents);
        if (absolute <= 25)
        {
            return "normal";
        }

        if (absolute <= 50)
        {
            return "warning";
        }

        return "critical";
    }

    private static List<PitchCurvePointDto> BuildPitchPointSeries(IReadOnlyList<PitchPoint> points)
    {
        return DownsampleSeries(points.Count, 120, index =>
        {
            var point = points[index];
            return new PitchCurvePointDto
            {
                TimeSeconds = Math.Round(point.TimeSeconds, 3),
                Value = Math.Round(FrequencyToMidi(point.FrequencyHz), 2)
            };
        });
    }

    private static List<PitchCurvePointDto> BuildDeviationSeries(IReadOnlyList<PitchMatch> matches)
    {
        return DownsampleSeries(matches.Count, 120, index =>
        {
            var match = matches[index];
            return new PitchCurvePointDto
            {
                TimeSeconds = Math.Round(match.TimeSeconds, 3),
                Value = Math.Round(match.DeviationCents, 2)
            };
        });
    }

    private static List<PitchCurvePointDto> DownsampleSeries(int count, int limit, Func<int, PitchCurvePointDto> selector)
    {
        var series = new List<PitchCurvePointDto>();
        if (count == 0)
        {
            return series;
        }

        var outputCount = Math.Min(limit, count);
        for (var index = 0; index < outputCount; index++)
        {
            var sourceIndex = outputCount == 1
                ? 0
                : (int)Math.Round(index * (count - 1.0) / (outputCount - 1.0));
            series.Add(selector(sourceIndex));
        }

        return series;
    }

    private static bool IsEnglish(EvaluationOptionsRequest options)
    {
        return string.Equals(options.FeedbackLanguage, "en-US", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PitchPoint(double TimeSeconds, double FrequencyHz);

    private sealed record PitchMatch(
        double TimeSeconds,
        double PerformanceFrequencyHz,
        double ReferenceFrequencyHz,
        double DeviationCents);

    private sealed record KeyDetectionResult(string Key, string Mode, double? ReferenceMedianMidi);
}
