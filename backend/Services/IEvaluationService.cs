using backend.Models;

namespace backend.Services;

public interface IEvaluationService
{
    Task<EvaluationSubmitResponse> SubmitAsync(
        IFormFile performanceFile,
        IFormFile? referenceFile,
        EvaluationOptionsRequest options,
        int? userId,
        CancellationToken cancellationToken = default);

    Task<EvaluationSubmitResponse> CreateAsync(
        CreateEvaluationRequest request,
        int? userId,
        CancellationToken cancellationToken = default);

    Task<EvaluationStatusResponse> GetStatusAsync(
        string evaluationId,
        int? userId,
        string? accessToken,
        CancellationToken cancellationToken = default);

    Task<EvaluationReportResponse> GetReportAsync(
        string evaluationId,
        int? userId,
        string? accessToken,
        CancellationToken cancellationToken = default);

    Task<TransposeSuggestionResponse> GetTransposeSuggestionAsync(
        string evaluationId,
        int? userId,
        string? accessToken,
        TransposeSuggestionRequest request,
        CancellationToken cancellationToken = default);

    Task<EvaluationHistoryResponse> GetHistoryAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<EvaluationExportResponse> ExportAsync(
        string evaluationId,
        int userId,
        string format,
        CancellationToken cancellationToken = default);

    Task ProcessQueuedEvaluationAsync(int evaluationDbId, CancellationToken cancellationToken = default);
}

public interface IAudioPreparationService
{
    Task<PreparedAudioResult> PrepareAsync(MediaFile mediaFile, CancellationToken cancellationToken = default);
}

public interface IPitchAnalysisService
{
    PitchAnalysisResult Analyze(string performancePath, string? referencePath, EvaluationOptionsRequest options);
}

public interface IRhythmEvaluationService
{
    RhythmEvaluationResult Analyze(string performancePath, string? referencePath, EvaluationOptionsRequest options);
}

public interface IEvaluationScoringService
{
    EvaluationAggregateResult Score(
        PitchAnalysisResult pitchResult,
        RhythmEvaluationResult rhythmResult,
        EvaluationOptionsRequest options);
}

public interface IEvaluationTaskQueue
{
    ValueTask QueueAsync(int evaluationDbId, CancellationToken cancellationToken = default);
    ValueTask<int> DequeueAsync(CancellationToken cancellationToken = default);
}

public interface IAnonymousEvaluationAccessTokenService
{
    string GenerateToken();
    string HashToken(string token);
    bool ValidateToken(string expectedHash, string providedToken);
}

public sealed class EvaluationProcessingOptions
{
    public int ImmediateProcessingMaxDurationSeconds { get; set; } = 45;
    public double PitchWeight { get; set; } = 0.6;
    public double RhythmWeight { get; set; } = 0.4;
}

public sealed class EvaluationExecutionPlanner
{
    private readonly EvaluationProcessingOptions _options;

    public EvaluationExecutionPlanner(EvaluationProcessingOptions options)
    {
        _options = options;
    }

    public bool ShouldProcessSynchronously(
        MediaFile performanceMedia,
        MediaFile? referenceMedia,
        EvaluationOptionsRequest options)
    {
        if (!options.AnalyzePitch && !options.AnalyzeRhythm)
        {
            return true;
        }

        var maxDurationMs = _options.ImmediateProcessingMaxDurationSeconds * 1000;
        if (!IsReady(performanceMedia) || performanceMedia.DurationMs is null)
        {
            return false;
        }

        if (performanceMedia.DurationMs > maxDurationMs)
        {
            return false;
        }

        if (referenceMedia == null)
        {
            return true;
        }

        if (options.AnalyzePitch && !IsReady(referenceMedia))
        {
            return false;
        }

        return referenceMedia.DurationMs is null || referenceMedia.DurationMs <= maxDurationMs;
    }

    private static bool IsReady(MediaFile mediaFile)
    {
        return string.Equals(mediaFile.PreparedAudioStatus, "ready", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class PreparedAudioResult
{
    public string Status { get; set; } = "failed";
    public string? AbsolutePath { get; set; }
    public int? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class PitchAnalysisResult
{
    public string Status { get; set; } = "pending";
    public double? Score { get; set; }
    public double? MeanDeviationCents { get; set; }
    public double? HitRate25 { get; set; }
    public double? HitRate50 { get; set; }
    public double? Coverage { get; set; }
    public double? Consistency { get; set; }
    public string? DetectedKey { get; set; }
    public string? DetectedMode { get; set; }
    public double? ReferenceMedianMidi { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<PitchCurvePointDto> ReferencePoints { get; set; } = new();
    public List<PitchCurvePointDto> PerformancePoints { get; set; } = new();
    public List<PitchCurvePointDto> DeviationPoints { get; set; } = new();
    public List<EvaluationSegmentDto> Segments { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class RhythmEvaluationResult
{
    public string Status { get; set; } = "pending";
    public double? Score { get; set; }
    public double? PerformanceTempoBpm { get; set; }
    public double? ReferenceTempoBpm { get; set; }
    public double? Coverage { get; set; }
    public double? Consistency { get; set; }
    public double? AverageDeviationMs { get; set; }
    public int ThresholdMs { get; set; } = 50;
    public SeverityCountDto SeverityCounts { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public List<EvaluationSegmentDto> Segments { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class EvaluationAggregateResult
{
    public string Status { get; set; } = "failed";
    public string ScoringProfile { get; set; } = "unavailable";
    public string PitchStatus { get; set; } = "failed";
    public string RhythmStatus { get; set; } = "failed";
    public double? TotalScore { get; set; }
    public string Badge { get; set; } = "未完成";
    public string SummaryText { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public List<EvaluationSuggestionDto> Suggestions { get; set; } = new();
}
