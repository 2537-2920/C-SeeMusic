using backend.Models;

namespace backend.Services;

public sealed class RhythmEvaluationService : IRhythmEvaluationService
{
    private readonly IBeatAnalysisService _beatAnalysisService;

    public RhythmEvaluationService(IBeatAnalysisService beatAnalysisService)
    {
        _beatAnalysisService = beatAnalysisService;
    }

    public RhythmEvaluationResult Analyze(string performancePath, string? referencePath, EvaluationOptionsRequest options)
    {
        var normalizedOptions = (options ?? new EvaluationOptionsRequest()).Normalize();
        var isEnglish = string.Equals(normalizedOptions.FeedbackLanguage, "en-US", StringComparison.OrdinalIgnoreCase);
        var thresholdMs = normalizedOptions.RhythmThresholdMs;

        var performanceAnalysis = _beatAnalysisService.AnalyzeFile(performancePath);
        if (!performanceAnalysis.IsAvailable)
        {
            return new RhythmEvaluationResult
            {
                Status = "failed",
                ThresholdMs = thresholdMs,
                Summary = performanceAnalysis.Summary,
            };
        }

        if (string.IsNullOrWhiteSpace(referencePath))
        {
            return new RhythmEvaluationResult
            {
                Status = "failed",
                ThresholdMs = thresholdMs,
                Summary = isEnglish
                    ? "No reference audio was provided, so the comparison rhythm evaluation could not continue."
                    : "未上传标准音频，无法继续进行节奏对比评估。",
            };
        }

        if (!File.Exists(referencePath))
        {
            return new RhythmEvaluationResult
            {
                Status = "failed",
                ThresholdMs = thresholdMs,
                Summary = isEnglish
                    ? "The reference audio file is missing, so the comparison rhythm evaluation could not continue."
                    : "标准音频不存在，无法继续进行节奏对比评估。",
            };
        }

        var referenceAnalysis = _beatAnalysisService.AnalyzeFile(referencePath);
        if (!referenceAnalysis.IsAvailable)
        {
            return new RhythmEvaluationResult
            {
                Status = "failed",
                ThresholdMs = thresholdMs,
                Summary = isEnglish
                    ? "The reference audio did not contain stable beat markers, so the comparison rhythm evaluation could not continue."
                    : "标准音频未提取到稳定拍点，无法继续进行节奏对比评估。",
            };
        }

        var segments = BuildSegments(
            performanceAnalysis.BeatTimes,
            referenceAnalysis.BeatTimes,
            thresholdMs,
            isEnglish);

        var severityCounts = new SeverityCountDto
        {
            Normal = segments.Count(segment => segment.Severity == "normal"),
            Warning = segments.Count(segment => segment.Severity == "warning"),
            Critical = segments.Count(segment => segment.Severity == "critical"),
        };

        var coverage = Math.Round(Math.Min(performanceAnalysis.BeatTimes.Count, referenceAnalysis.BeatTimes.Count)
            / (double)Math.Max(1, Math.Max(performanceAnalysis.BeatTimes.Count, referenceAnalysis.BeatTimes.Count)) * 100.0, 1);
        var relativeStability = referenceAnalysis.Stability > 1e-9
            ? Math.Clamp(performanceAnalysis.Stability / referenceAnalysis.Stability, 0.0, 1.0)
            : 1.0;
        var consistency = Math.Round(relativeStability * 100.0, 1);
        var averageDeviationMs = segments.Count == 0
            ? 0.0
            : Math.Round(segments.Where(segment => segment.DeviationValue.HasValue).Average(segment => segment.DeviationValue!.Value), 1);
        var timingScore = segments.Count == 0
            ? Math.Clamp(100.0 - averageDeviationMs / Math.Max(20.0, thresholdMs) * 30.0, 0.0, 100.0)
            : Math.Clamp(segments.Average(segment => segment.Score ?? 0.0), 0.0, 100.0);
        var score = Math.Round(
            relativeStability * 100.0 * 0.25
            + coverage * 0.25
            + timingScore * 0.50,
            1);

        return new RhythmEvaluationResult
        {
            Status = "succeeded",
            Score = score,
            PerformanceTempoBpm = performanceAnalysis.TempoBpm,
            ReferenceTempoBpm = referenceAnalysis?.TempoBpm,
            Coverage = coverage,
            Consistency = consistency,
            AverageDeviationMs = averageDeviationMs,
            ThresholdMs = thresholdMs,
            SeverityCounts = severityCounts,
            Summary = isEnglish
                ? $"The performance tempo is about {performanceAnalysis.TempoBpm:F1} BPM and has been aligned against the reference beat grid."
                : $"节奏约 {performanceAnalysis.TempoBpm:F1} BPM，已结合参考拍点对齐评分。",
            Segments = segments,
            Warnings = new List<string>(),
        };
    }

    private static List<EvaluationSegmentDto> BuildSegments(
        IReadOnlyList<double> performanceBeatTimes,
        IReadOnlyList<double>? referenceBeatTimes,
        int thresholdMs,
        bool isEnglish)
    {
        var segments = new List<EvaluationSegmentDto>();
        if (performanceBeatTimes.Count < 2)
        {
            return segments;
        }

        var useReference = referenceBeatTimes != null && referenceBeatTimes.Count >= 2;
        var performanceIntervals = BuildIntervals(performanceBeatTimes);
        var referenceIntervals = useReference ? BuildIntervals(referenceBeatTimes!) : Array.Empty<double>();
        var baselineInterval = performanceIntervals.Average();

        for (var index = 0; index < performanceIntervals.Length; index++)
        {
            var deviationMs = useReference && index < referenceIntervals.Length
                ? Math.Abs(performanceIntervals[index] - referenceIntervals[index]) * 1000.0
                : Math.Abs(performanceIntervals[index] - baselineInterval) * 1000.0;
            var severity = GetSeverity(deviationMs, thresholdMs);
            segments.Add(new EvaluationSegmentDto
            {
                MetricType = "rhythm",
                StartMs = (int)Math.Round(performanceBeatTimes[index] * 1000),
                EndMs = (int)Math.Round(performanceBeatTimes[index + 1] * 1000),
                Score = Math.Round(Math.Clamp(100.0 - deviationMs / Math.Max(20.0, thresholdMs) * 25.0, 0.0, 100.0), 1),
                DeviationValue = Math.Round(deviationMs, 1),
                DeviationUnit = "ms",
                Severity = severity,
                NoteText = BuildNoteText(deviationMs, thresholdMs, isEnglish)
            });
        }

        return segments.Take(16).ToList();
    }

    private static string BuildNoteText(double deviationMs, int thresholdMs, bool isEnglish)
    {
        if (deviationMs <= thresholdMs)
        {
            return isEnglish
                ? "This beat stays close to the expected duration."
                : "这一拍的时值控制较稳定。";
        }

        if (deviationMs <= thresholdMs * 2.5)
        {
            return isEnglish
                ? "This beat is slightly ahead or behind the reference pulse."
                : "这一拍存在轻微抢拍或拖拍。";
        }

        return isEnglish
            ? "This beat drifts clearly from the target pulse and should be practiced with a metronome."
            : "这一拍偏移明显，建议配合节拍器拆拍练习。";
    }

    private static double[] BuildIntervals(IReadOnlyList<double> beatTimes)
    {
        var intervals = new double[beatTimes.Count - 1];
        for (var index = 1; index < beatTimes.Count; index++)
        {
            intervals[index - 1] = beatTimes[index] - beatTimes[index - 1];
        }

        return intervals;
    }

    private static string GetSeverity(double deviationMs, int thresholdMs)
    {
        if (deviationMs <= thresholdMs)
        {
            return "normal";
        }

        if (deviationMs <= thresholdMs * 2.5)
        {
            return "warning";
        }

        return "critical";
    }
}
