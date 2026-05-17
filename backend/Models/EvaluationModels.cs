using System.Collections.Generic;

namespace backend.Models;

public sealed class EvaluationOptionsRequest
{
    public bool AnalyzePitch { get; set; } = true;
    public bool AnalyzeRhythm { get; set; } = true;
    public string UserAudioType { get; set; } = "with_accompaniment";
    public string FeedbackLanguage { get; set; } = "zh-CN";
    public string ScoringModel { get; set; } = "balanced";
    public int RhythmThresholdMs { get; set; } = 50;

    public EvaluationOptionsRequest Normalize()
    {
        return new EvaluationOptionsRequest
        {
            AnalyzePitch = AnalyzePitch,
            AnalyzeRhythm = AnalyzeRhythm,
            UserAudioType = string.Equals(UserAudioType, "clean_vocal", StringComparison.OrdinalIgnoreCase)
                ? "clean_vocal"
                : "with_accompaniment",
            FeedbackLanguage = string.Equals(FeedbackLanguage, "en-US", StringComparison.OrdinalIgnoreCase)
                ? "en-US"
                : "zh-CN",
            ScoringModel = ScoringModel?.ToLowerInvariant() switch
            {
                "pitch_focus" => "pitch_focus",
                "rhythm_focus" => "rhythm_focus",
                _ => "balanced",
            },
            RhythmThresholdMs = Math.Clamp(RhythmThresholdMs <= 0 ? 50 : RhythmThresholdMs, 20, 200)
        };
    }
}

public sealed class CreateEvaluationRequest
{
    public string PerformanceMediaId { get; set; } = string.Empty;
    public string? ReferenceMediaId { get; set; }
    public EvaluationOptionsRequest Options { get; set; } = new();
}

public sealed class EvaluationSubmitResponse
{
    public string EvaluationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public EvaluationReportResponse? ReportPreview { get; set; }
    public string? AnonymousAccessToken { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public sealed class EvaluationStatusResponse
{
    public string EvaluationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string ScoringProfile { get; set; } = string.Empty;
    public double? TotalScore { get; set; }
    public string PitchStatus { get; set; } = string.Empty;
    public string RhythmStatus { get; set; } = string.Empty;
    public string FeedbackLanguage { get; set; } = "zh-CN";
    public string ScoringModel { get; set; } = "balanced";
    public List<string> Warnings { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public sealed class EvaluationReportResponse
{
    public EvaluationSummaryDto Summary { get; set; } = new();
    public PitchAnalysisDto PitchAnalysis { get; set; } = new();
    public RhythmAnalysisDto RhythmAnalysis { get; set; } = new();
    public TransposeBaseDto TransposeBase { get; set; } = new();
    public List<EvaluationSuggestionDto> Suggestions { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public List<EvaluationSegmentDto> PitchSegments
    {
        get => PitchAnalysis.Segments;
        set => PitchAnalysis.Segments = value ?? new List<EvaluationSegmentDto>();
    }

    public List<EvaluationSegmentDto> RhythmSegments
    {
        get => RhythmAnalysis.Segments;
        set => RhythmAnalysis.Segments = value ?? new List<EvaluationSegmentDto>();
    }
}

public sealed class EvaluationSummaryDto
{
    public string AnalysisId { get; set; } = string.Empty;
    public string ReferenceFileName { get; set; } = string.Empty;
    public double? PerformanceTempoBpm { get; set; }
    public double? ReferenceTempoBpm { get; set; }
    public double? TotalScore { get; set; }
    public string Badge { get; set; } = string.Empty;
    public string ScoringProfile { get; set; } = string.Empty;
    public double? PitchScore { get; set; }
    public double? RhythmScore { get; set; }
    public double? Coverage { get; set; }
    public double? Consistency { get; set; }
    public double? MeanPitchDeviationCents { get; set; }
    public string PitchStatus { get; set; } = string.Empty;
    public string RhythmStatus { get; set; } = string.Empty;
    public string FeedbackLanguage { get; set; } = "zh-CN";
    public string SummaryText { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

public sealed class PitchAnalysisDto
{
    public double? HitRate25 { get; set; }
    public double? HitRate50 { get; set; }
    public double? MeanDeviationCents { get; set; }
    public double? Coverage { get; set; }
    public double? Consistency { get; set; }
    public List<PitchCurvePointDto> ReferencePoints { get; set; } = new();
    public List<PitchCurvePointDto> PerformancePoints { get; set; } = new();
    public List<PitchCurvePointDto> DeviationPoints { get; set; } = new();
    public List<EvaluationSegmentDto> Segments { get; set; } = new();
}

public sealed class PitchCurvePointDto
{
    public double TimeSeconds { get; set; }
    public double Value { get; set; }
}

public sealed class RhythmAnalysisDto
{
    public int ThresholdMs { get; set; } = 50;
    public double? PerformanceTempoBpm { get; set; }
    public double? ReferenceTempoBpm { get; set; }
    public double? Coverage { get; set; }
    public double? Consistency { get; set; }
    public double? AverageDeviationMs { get; set; }
    public SeverityCountDto SeverityCounts { get; set; } = new();
    public List<EvaluationSegmentDto> Segments { get; set; } = new();
}

public sealed class SeverityCountDto
{
    public int Normal { get; set; }
    public int Warning { get; set; }
    public int Critical { get; set; }
}

public sealed class TransposeBaseDto
{
    public string DetectedKey { get; set; } = "--";
    public string DetectedMode { get; set; } = "--";
    public double? ReferenceMedianMidi { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public sealed class EvaluationSegmentDto
{
    public string MetricType { get; set; } = string.Empty;
    public int StartMs { get; set; }
    public int EndMs { get; set; }
    public double? Score { get; set; }
    public double? DeviationValue { get; set; }
    public string? DeviationUnit { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
}

public sealed class EvaluationSuggestionDto
{
    public string SuggestionType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class TransposeSuggestionRequest
{
    public string SourceGender { get; set; } = "male";
    public string TargetGender { get; set; } = "female";
    public string FeedbackLanguage { get; set; } = "zh-CN";
    public TransposeBaseDto? TransposeBase { get; set; }
}

public sealed class TransposeSuggestionResponse
{
    public string DetectedKey { get; set; } = "--";
    public string DetectedMode { get; set; } = "--";
    public int? RecommendedSemitone { get; set; }
    public string RecommendedKey { get; set; } = "--";
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Tips { get; set; } = new();
}

public sealed class EvaluationPdfExportRequest
{
    public EvaluationReportResponse Report { get; set; } = new();
}

public sealed class EvaluationHistoryResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<EvaluationHistoryItemDto> Items { get; set; } = new();
}

public sealed class EvaluationHistoryItemDto
{
    public string EvaluationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double? TotalScore { get; set; }
    public double? PitchScore { get; set; }
    public double? RhythmScore { get; set; }
    public string ScoringProfile { get; set; } = string.Empty;
    public string PerformanceFileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class ExportEvaluationRequest
{
    public string Format { get; set; } = "pdf";
}

public sealed class EvaluationExportResponse
{
    public string EvaluationId { get; set; } = string.Empty;
    public string ExportType { get; set; } = "pdf";
    public string FileName { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
