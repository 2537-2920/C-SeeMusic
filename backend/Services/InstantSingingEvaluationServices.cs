using System.ComponentModel;
using System.Diagnostics;
using backend.Models;

namespace backend.Services;

public sealed class InstantSingingEvaluationService : IInstantSingingEvaluationService
{
    private readonly ITemporaryAudioPreparationService _temporaryAudioPreparationService;
    private readonly IPitchAnalysisService _pitchAnalysisService;
    private readonly IRhythmEvaluationService _rhythmEvaluationService;
    private readonly IEvaluationScoringService _evaluationScoringService;

    public InstantSingingEvaluationService(
        ITemporaryAudioPreparationService temporaryAudioPreparationService,
        IPitchAnalysisService pitchAnalysisService,
        IRhythmEvaluationService rhythmEvaluationService,
        IEvaluationScoringService evaluationScoringService)
    {
        _temporaryAudioPreparationService = temporaryAudioPreparationService;
        _pitchAnalysisService = pitchAnalysisService;
        _rhythmEvaluationService = rhythmEvaluationService;
        _evaluationScoringService = evaluationScoringService;
    }

    public async Task<EvaluationReportResponse> EvaluateAsync(
        IFormFile performanceFile,
        IFormFile referenceFile,
        EvaluationOptionsRequest options,
        CancellationToken cancellationToken = default)
    {
        var normalizedOptions = (options ?? new EvaluationOptionsRequest()).Normalize();
        if (performanceFile == null || performanceFile.Length == 0)
        {
            throw new InvalidOperationException(LocalizedText.MissingPerformance(normalizedOptions));
        }

        if (referenceFile == null || referenceFile.Length == 0)
        {
            throw new InvalidOperationException(LocalizedText.ReferenceRequired(normalizedOptions));
        }

        TemporaryPreparedAudioResult? performanceAudio = null;
        TemporaryPreparedAudioResult? referenceAudio = null;
        var cleanupDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            performanceAudio = await _temporaryAudioPreparationService.PrepareAsync(performanceFile, cancellationToken);
            RegisterCleanupDirectory(cleanupDirectories, performanceAudio.WorkingDirectory);
            EnsurePreparedAudioReady(
                performanceAudio,
                performanceFile.FileName,
                normalizedOptions,
                isReference: false);

            referenceAudio = await _temporaryAudioPreparationService.PrepareAsync(referenceFile, cancellationToken);
            RegisterCleanupDirectory(cleanupDirectories, referenceAudio.WorkingDirectory);
            EnsurePreparedAudioReady(
                referenceAudio,
                referenceFile.FileName,
                normalizedOptions,
                isReference: true);

            PitchAnalysisResult pitchResult;
            if (normalizedOptions.AnalyzePitch)
            {
                pitchResult = _pitchAnalysisService.Analyze(
                    performanceAudio.AbsolutePath!,
                    referenceAudio.AbsolutePath!,
                    normalizedOptions);
                if (!string.Equals(pitchResult.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(pitchResult.Summary);
                }
            }
            else
            {
                pitchResult = new PitchAnalysisResult
                {
                    Status = "skipped",
                    Summary = LocalizedText.PitchDisabled(normalizedOptions),
                };
            }

            RhythmEvaluationResult rhythmResult;
            if (normalizedOptions.AnalyzeRhythm)
            {
                rhythmResult = _rhythmEvaluationService.Analyze(
                    performanceAudio.AbsolutePath!,
                    referenceAudio.AbsolutePath!,
                    normalizedOptions);
                if (!string.Equals(rhythmResult.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(rhythmResult.Summary);
                }
            }
            else
            {
                rhythmResult = new RhythmEvaluationResult
                {
                    Status = "skipped",
                    ThresholdMs = normalizedOptions.RhythmThresholdMs,
                    Summary = LocalizedText.RhythmDisabled(normalizedOptions),
                };
            }

            var aggregate = _evaluationScoringService.Score(pitchResult, rhythmResult, normalizedOptions);
            if (!string.Equals(aggregate.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(aggregate.ErrorMessage)
                    ? aggregate.SummaryText
                    : aggregate.ErrorMessage);
            }

            var warnings = aggregate.Warnings
                .Concat(pitchResult.Warnings)
                .Concat(rhythmResult.Warnings)
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Distinct()
                .ToList();
            var generatedAt = DateTime.UtcNow;

            return new EvaluationReportResponse
            {
                Summary = new EvaluationSummaryDto
                {
                    AnalysisId = Guid.NewGuid().ToString("N"),
                    ReferenceFileName = Path.GetFileName(referenceFile.FileName),
                    PerformanceTempoBpm = rhythmResult.PerformanceTempoBpm,
                    ReferenceTempoBpm = rhythmResult.ReferenceTempoBpm,
                    TotalScore = aggregate.TotalScore,
                    Badge = aggregate.Badge,
                    ScoringProfile = aggregate.ScoringProfile,
                    PitchScore = pitchResult.Score,
                    RhythmScore = rhythmResult.Score,
                    Coverage = ResolveCombinedMetric(pitchResult.Coverage, rhythmResult.Coverage),
                    Consistency = ResolveCombinedMetric(pitchResult.Consistency, rhythmResult.Consistency),
                    MeanPitchDeviationCents = pitchResult.MeanDeviationCents,
                    PitchStatus = aggregate.PitchStatus,
                    RhythmStatus = aggregate.RhythmStatus,
                    FeedbackLanguage = normalizedOptions.FeedbackLanguage,
                    SummaryText = aggregate.SummaryText,
                    GeneratedAt = generatedAt,
                },
                PitchAnalysis = BuildPitchAnalysisDto(pitchResult),
                RhythmAnalysis = BuildRhythmAnalysisDto(rhythmResult),
                TransposeBase = InstantTransposeSuggestionService.BuildTransposeBase(pitchResult, normalizedOptions),
                Suggestions = aggregate.Suggestions,
                Warnings = warnings,
            };
        }
        finally
        {
            foreach (var directory in cleanupDirectories)
            {
                try
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                }
                catch (IOException)
                {
                    // Ignore cleanup errors for request-scoped temp files.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore cleanup errors for request-scoped temp files.
                }
            }
        }
    }

    private static void EnsurePreparedAudioReady(
        TemporaryPreparedAudioResult preparedAudio,
        string originalFileName,
        EvaluationOptionsRequest options,
        bool isReference)
    {
        if (preparedAudio == null
            || !string.Equals(preparedAudio.Status, "ready", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(preparedAudio.AbsolutePath))
        {
            var fallback = isReference
                ? LocalizedText.ReferencePreparationFailed(options)
                : LocalizedText.PerformancePreparationFailed(options);
            var fileRole = isReference ? "标准音频" : "演唱音频";
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(preparedAudio?.ErrorMessage)
                ? $"{fileRole} {Path.GetFileName(originalFileName)} 准备失败。{fallback}"
                : preparedAudio.ErrorMessage);
        }
    }

    private static void RegisterCleanupDirectory(ISet<string> cleanupDirectories, string? directory)
    {
        if (!string.IsNullOrWhiteSpace(directory))
        {
            cleanupDirectories.Add(directory);
        }
    }

    private static PitchAnalysisDto BuildPitchAnalysisDto(PitchAnalysisResult result)
    {
        return new PitchAnalysisDto
        {
            HitRate25 = result.HitRate25,
            HitRate50 = result.HitRate50,
            MeanDeviationCents = result.MeanDeviationCents,
            Coverage = result.Coverage,
            Consistency = result.Consistency,
            ReferencePoints = result.ReferencePoints,
            PerformancePoints = result.PerformancePoints,
            DeviationPoints = result.DeviationPoints,
            Segments = result.Segments,
        };
    }

    private static RhythmAnalysisDto BuildRhythmAnalysisDto(RhythmEvaluationResult result)
    {
        return new RhythmAnalysisDto
        {
            ThresholdMs = result.ThresholdMs,
            PerformanceTempoBpm = result.PerformanceTempoBpm,
            ReferenceTempoBpm = result.ReferenceTempoBpm,
            Coverage = result.Coverage,
            Consistency = result.Consistency,
            AverageDeviationMs = result.AverageDeviationMs,
            SeverityCounts = result.SeverityCounts,
            Segments = result.Segments,
        };
    }

    private static double? ResolveCombinedMetric(double? pitchMetric, double? rhythmMetric)
    {
        if (pitchMetric.HasValue && rhythmMetric.HasValue)
        {
            return Math.Round((pitchMetric.Value + rhythmMetric.Value) / 2.0, 1);
        }

        if (pitchMetric.HasValue)
        {
            return Math.Round(pitchMetric.Value, 1);
        }

        if (rhythmMetric.HasValue)
        {
            return Math.Round(rhythmMetric.Value, 1);
        }

        return null;
    }
}

public sealed class TemporaryAudioPreparationService : ITemporaryAudioPreparationService
{
    public async Task<TemporaryPreparedAudioResult> PrepareAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "seemusic-singing-evaluation",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var sourcePath = Path.Combine(workingDirectory, "source" + extension);

        try
        {
            await using (var stream = File.Create(sourcePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            if (IsWaveFile(file))
            {
                var audioData = WavAudioReader.Read(sourcePath);
                return new TemporaryPreparedAudioResult
                {
                    Status = "ready",
                    AbsolutePath = sourcePath,
                    WorkingDirectory = workingDirectory,
                    DurationMs = (int)Math.Round(audioData.DurationSeconds * 1000),
                };
            }

            var preparedPath = Path.Combine(workingDirectory, "prepared.wav");
            var errorOutput = await ConvertToWaveAsync(sourcePath, preparedPath, cancellationToken);
            if (!File.Exists(preparedPath))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorOutput)
                    ? "ffmpeg 转码失败，无法生成标准 WAV 素材。"
                    : $"ffmpeg 转码失败：{errorOutput}");
            }

            var preparedAudio = WavAudioReader.Read(preparedPath);
            return new TemporaryPreparedAudioResult
            {
                Status = "ready",
                AbsolutePath = preparedPath,
                WorkingDirectory = workingDirectory,
                DurationMs = (int)Math.Round(preparedAudio.DurationSeconds * 1000),
            };
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or Win32Exception
            or InvalidDataException
            or NotSupportedException)
        {
            return new TemporaryPreparedAudioResult
            {
                Status = "failed",
                WorkingDirectory = workingDirectory,
                ErrorMessage = exception is Win32Exception
                    ? "当前环境未找到 ffmpeg，无法自动转码非 WAV 素材。"
                    : exception.Message,
            };
        }
    }

    private static async Task<string> ConvertToWaveAsync(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -i \"{sourcePath}\" -ac 1 -ar 44100 -sample_fmt s16 \"{targetPath}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("无法启动 ffmpeg 进行音频转码。");
        }

        _ = process.StandardOutput.ReadToEndAsync();
        var errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorOutput)
                ? "ffmpeg 转码失败，无法生成标准 WAV 素材。"
                : $"ffmpeg 转码失败：{errorOutput.Trim()}");
        }

        return errorOutput.Trim();
    }

    private static bool IsWaveFile(IFormFile file)
    {
        return string.Equals(Path.GetExtension(file.FileName), ".wav", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.ContentType, "audio/wav", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.ContentType, "audio/x-wav", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class InstantTransposeSuggestionService : ITransposeSuggestionService
{
    public TransposeSuggestionResponse Build(TransposeSuggestionRequest request)
    {
        if (request?.TransposeBase == null)
        {
            throw new InvalidOperationException("缺少当前评估报告的调性基础信息，无法生成变调建议。");
        }

        var options = new EvaluationOptionsRequest
        {
            FeedbackLanguage = request.FeedbackLanguage,
        }.Normalize();

        return BuildTransposeSuggestion(request.TransposeBase, request, options);
    }

    internal static TransposeBaseDto BuildTransposeBase(
        PitchAnalysisResult pitchResult,
        EvaluationOptionsRequest options)
    {
        if (string.IsNullOrWhiteSpace(pitchResult.DetectedKey) || pitchResult.DetectedKey == "--")
        {
            return new TransposeBaseDto
            {
                Summary = LocalizedText.TransposeUnstable(options)
            };
        }

        return new TransposeBaseDto
        {
            DetectedKey = pitchResult.DetectedKey,
            DetectedMode = pitchResult.DetectedMode ?? "--",
            ReferenceMedianMidi = pitchResult.ReferenceMedianMidi,
            Summary = LocalizedText.TransposeDetected(
                options,
                pitchResult.DetectedKey,
                pitchResult.DetectedMode ?? "--")
        };
    }

    private static TransposeSuggestionResponse BuildTransposeSuggestion(
        TransposeBaseDto transposeBase,
        TransposeSuggestionRequest request,
        EvaluationOptionsRequest options)
    {
        var isEnglish = string.Equals(options.FeedbackLanguage, "en-US", StringComparison.OrdinalIgnoreCase);
        var sourceGender = NormalizeGender(request.SourceGender, "male");
        var targetGender = NormalizeGender(request.TargetGender, "female");

        if (string.IsNullOrWhiteSpace(transposeBase.DetectedKey)
            || transposeBase.DetectedKey == "--"
            || !transposeBase.ReferenceMedianMidi.HasValue)
        {
            return new TransposeSuggestionResponse
            {
                DetectedKey = transposeBase.DetectedKey,
                DetectedMode = transposeBase.DetectedMode,
                Title = isEnglish ? "Key Suggestion Unavailable" : "暂时无法生成变调建议",
                Summary = isEnglish
                    ? "The reference melody was not stable enough to identify a dependable key center."
                    : "参考旋律暂时不够稳定，暂时无法识别可信的调性中心。",
                Tips =
                {
                    isEnglish
                        ? "Try a cleaner reference vocal or a melody-forward demo track."
                        : "建议换一段主旋律更清晰的参考音频后再试。",
                    isEnglish
                        ? "If the reference contains heavy accompaniment, upload a clearer standard take."
                        : "如果参考音频伴奏较重，建议上传更干净的标准音频。"
                }
            };
        }

        var targetCenterMidi = targetGender == "female" ? 62.0 : 57.0;
        var recommendedSemitone = (int)Math.Round(targetCenterMidi - transposeBase.ReferenceMedianMidi.Value);
        recommendedSemitone = Math.Clamp(recommendedSemitone, -6, 6);
        var recommendedKey = TransposeKey(transposeBase.DetectedKey, recommendedSemitone);
        var noChange = recommendedSemitone == 0;

        return new TransposeSuggestionResponse
        {
            DetectedKey = transposeBase.DetectedKey,
            DetectedMode = transposeBase.DetectedMode,
            RecommendedSemitone = recommendedSemitone,
            RecommendedKey = recommendedKey,
            Title = noChange
                ? (isEnglish ? "Current Key Already Fits" : "当前调性已经比较合适")
                : (isEnglish ? "Suggested Key Shift" : "推荐变调建议"),
            Summary = BuildTransposeSummary(
                transposeBase,
                sourceGender,
                targetGender,
                recommendedSemitone,
                recommendedKey,
                isEnglish),
            Tips = BuildTransposeTips(sourceGender, targetGender, recommendedSemitone, isEnglish),
        };
    }

    private static string BuildTransposeSummary(
        TransposeBaseDto transposeBase,
        string sourceGender,
        string targetGender,
        int recommendedSemitone,
        string recommendedKey,
        bool isEnglish)
    {
        if (recommendedSemitone == 0)
        {
            return isEnglish
                ? $"The detected key is {transposeBase.DetectedKey} {transposeBase.DetectedMode}. The current tessitura already fits the selected voice line."
                : $"检测到当前调性为 {transposeBase.DetectedKey} {FormatMode(transposeBase.DetectedMode)}，当前音域与目标声线已经比较匹配。";
        }

        var directionText = recommendedSemitone > 0
            ? (isEnglish ? $"raise by {recommendedSemitone} semitone(s)" : $"升 {recommendedSemitone} 个半音")
            : (isEnglish ? $"lower by {Math.Abs(recommendedSemitone)} semitone(s)" : $"降 {Math.Abs(recommendedSemitone)} 个半音");

        return isEnglish
            ? $"The detected key is {transposeBase.DetectedKey} {transposeBase.DetectedMode}. To move from the {sourceGender} line toward the {targetGender} line, {directionText} to {recommendedKey}."
            : $"检测到当前调性为 {transposeBase.DetectedKey} {FormatMode(transposeBase.DetectedMode)}，若从{FormatGender(sourceGender, false)}切换到{FormatGender(targetGender, false)}，建议{directionText}到 {recommendedKey}。";
    }

    private static List<string> BuildTransposeTips(string sourceGender, string targetGender, int recommendedSemitone, bool isEnglish)
    {
        var tips = new List<string>();
        if (recommendedSemitone == 0)
        {
            tips.Add(isEnglish
                ? "Keep the current key and focus on phrasing and resonance rather than transposition."
                : "建议保持当前调性，把重点放在共鸣位置和乐句表达上。");
        }
        else
        {
            tips.Add(isEnglish
                ? "Test the new key on the chorus first to verify the high notes stay comfortable."
                : "建议先用副歌高点试唱新调性，确认高音区是否更舒适。");
            tips.Add(isEnglish
                ? "If the timbre becomes too thin or too heavy, fine-tune by another semitone."
                : "如果变调后音色明显发薄或发闷，可以再微调 1 个半音。");
        }

        tips.Add(isEnglish
            ? $"Selected path: {sourceGender} -> {targetGender}."
            : $"当前建议路径：{FormatGender(sourceGender, true)} -> {FormatGender(targetGender, true)}。");
        return tips;
    }

    private static string NormalizeGender(string? value, string fallback)
    {
        if (string.Equals(value, "female", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "女声", StringComparison.OrdinalIgnoreCase))
        {
            return "female";
        }

        if (string.Equals(value, "male", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "男声", StringComparison.OrdinalIgnoreCase))
        {
            return "male";
        }

        return fallback;
    }

    private static string FormatGender(string gender, bool shortLabel)
    {
        return gender == "female"
            ? (shortLabel ? "女声" : "女声线")
            : (shortLabel ? "男声" : "男声线");
    }

    private static string FormatMode(string? mode)
    {
        return string.Equals(mode, "minor", StringComparison.OrdinalIgnoreCase) ? "小调" : "大调";
    }

    private static string TransposeKey(string detectedKey, int semitoneShift)
    {
        var keys = new[] { "C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" };
        var index = Array.IndexOf(keys, detectedKey);
        if (index < 0)
        {
            return detectedKey;
        }

        var shifted = (index + semitoneShift) % keys.Length;
        if (shifted < 0)
        {
            shifted += keys.Length;
        }

        return keys[shifted];
    }
}

public sealed class PdfExportService : IPdfExportService
{
    public byte[] Export(EvaluationReportResponse report)
    {
        if (report?.Summary == null)
        {
            throw new InvalidOperationException("当前评估报告不可用，无法导出 PDF。");
        }

        var analysisId = string.IsNullOrWhiteSpace(report.Summary.AnalysisId)
            ? Guid.NewGuid().ToString("N")
            : report.Summary.AnalysisId;
        return MinimalPdfWriter.Write(report, analysisId);
    }
}
