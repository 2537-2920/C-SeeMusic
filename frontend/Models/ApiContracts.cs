using System;
using System.Collections.Generic;

namespace SeeMusicApp.Models
{
    public sealed class ApiResponse<T>
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }

    public sealed class MediaUploadResponse
    {
        public string MediaId { get; set; }
        public string FileName { get; set; }
        public string Url { get; set; }
        public string Type { get; set; }
    }

    public sealed class BeatAnalysisResult
    {
        public bool IsAvailable { get; set; }
        public double TempoBpm { get; set; }
        public List<double> BeatTimes { get; set; }
        public double Stability { get; set; }
        public double Confidence { get; set; }
        public int TimeSignatureNumerator { get; set; }
        public int TimeSignatureDenominator { get; set; }
        public string Summary { get; set; }
    }

    public sealed class TranscriptionResult
    {
        public string MediaId { get; set; }
        public string Status { get; set; }
        public string ScoreId { get; set; }
        public string Message { get; set; }
        public BeatAnalysisResult BeatAnalysis { get; set; }
    }

    public sealed class TranscriptionRequest
    {
        public string MediaId { get; set; }
        public bool SeparateMelody { get; set; }
        public bool SeparateAccompaniment { get; set; }
    }

    public sealed class AnalysisWorkflowResult
    {
        public MediaUploadResponse Upload { get; set; }
        public TranscriptionResult Analysis { get; set; }
    }

    public sealed class EvaluationSubmitResponse
    {
        public string EvaluationId { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public EvaluationReportResponse ReportPreview { get; set; }
        public string AnonymousAccessToken { get; set; }
        public List<string> Warnings { get; set; }
    }

    public sealed class EvaluationStatusResponse
    {
        public string EvaluationId { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public string ScoringProfile { get; set; }
        public double? TotalScore { get; set; }
        public string PitchStatus { get; set; }
        public string RhythmStatus { get; set; }
        public string FeedbackLanguage { get; set; }
        public string ScoringModel { get; set; }
        public List<string> Warnings { get; set; }
        public string ErrorMessage { get; set; }
    }

    public sealed class EvaluationReportResponse
    {
        public EvaluationSummary Summary { get; set; }
        public PitchAnalysis PitchAnalysis { get; set; }
        public RhythmAnalysis RhythmAnalysis { get; set; }
        public TransposeBase TransposeBase { get; set; }
        public List<EvaluationSuggestion> Suggestions { get; set; }
        public List<string> Warnings { get; set; }

        public List<EvaluationSegment> PitchSegments
        {
            get { return PitchAnalysis != null ? PitchAnalysis.Segments : null; }
            set
            {
                if (PitchAnalysis == null)
                {
                    PitchAnalysis = new PitchAnalysis();
                }

                PitchAnalysis.Segments = value;
            }
        }

        public List<EvaluationSegment> RhythmSegments
        {
            get { return RhythmAnalysis != null ? RhythmAnalysis.Segments : null; }
            set
            {
                if (RhythmAnalysis == null)
                {
                    RhythmAnalysis = new RhythmAnalysis();
                }

                RhythmAnalysis.Segments = value;
            }
        }
    }

    public sealed class EvaluationSummary
    {
        public string AnalysisId { get; set; }
        public string ReferenceFileName { get; set; }
        public double? PerformanceTempoBpm { get; set; }
        public double? ReferenceTempoBpm { get; set; }
        public double? TotalScore { get; set; }
        public string Badge { get; set; }
        public string ScoringProfile { get; set; }
        public double? PitchScore { get; set; }
        public double? RhythmScore { get; set; }
        public double? Coverage { get; set; }
        public double? Consistency { get; set; }
        public double? MeanPitchDeviationCents { get; set; }
        public string PitchStatus { get; set; }
        public string RhythmStatus { get; set; }
        public string FeedbackLanguage { get; set; }
        public string SummaryText { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public sealed class PitchAnalysis
    {
        public double? HitRate25 { get; set; }
        public double? HitRate50 { get; set; }
        public double? MeanDeviationCents { get; set; }
        public double? Coverage { get; set; }
        public double? Consistency { get; set; }
        public List<PitchCurvePoint> ReferencePoints { get; set; }
        public List<PitchCurvePoint> PerformancePoints { get; set; }
        public List<PitchCurvePoint> DeviationPoints { get; set; }
        public List<EvaluationSegment> Segments { get; set; }
    }

    public sealed class PitchCurvePoint
    {
        public double TimeSeconds { get; set; }
        public double Value { get; set; }
    }

    public sealed class RhythmAnalysis
    {
        public int ThresholdMs { get; set; }
        public double? PerformanceTempoBpm { get; set; }
        public double? ReferenceTempoBpm { get; set; }
        public double? Coverage { get; set; }
        public double? Consistency { get; set; }
        public double? AverageDeviationMs { get; set; }
        public SeverityCount SeverityCounts { get; set; }
        public List<EvaluationSegment> Segments { get; set; }
    }

    public sealed class SeverityCount
    {
        public int Normal { get; set; }
        public int Warning { get; set; }
        public int Critical { get; set; }
    }

    public sealed class TransposeBase
    {
        public string DetectedKey { get; set; }
        public string DetectedMode { get; set; }
        public double? ReferenceMedianMidi { get; set; }
        public string Summary { get; set; }
    }

    public sealed class EvaluationSegment
    {
        public string MetricType { get; set; }
        public int StartMs { get; set; }
        public int EndMs { get; set; }
        public double? Score { get; set; }
        public double? DeviationValue { get; set; }
        public string DeviationUnit { get; set; }
        public string Severity { get; set; }
        public string NoteText { get; set; }
    }

    public sealed class EvaluationSuggestion
    {
        public string SuggestionType { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
    }

    public sealed class TransposeSuggestionRequest
    {
        public string SourceGender { get; set; }
        public string TargetGender { get; set; }
    }

    public sealed class TransposeSuggestionResponse
    {
        public string DetectedKey { get; set; }
        public string DetectedMode { get; set; }
        public int? RecommendedSemitone { get; set; }
        public string RecommendedKey { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public List<string> Tips { get; set; }
    }

    public sealed class SingingEvaluationRequest
    {
        public string PerformanceFilePath { get; set; }
        public string ReferenceFilePath { get; set; }
        public string UserAudioType { get; set; }
        public string FeedbackLanguage { get; set; }
        public string ScoringModel { get; set; }
        public int RhythmThresholdMs { get; set; }
        public bool AnalyzePitch { get; set; }
        public bool AnalyzeRhythm { get; set; }
    }

    public sealed class EvaluationWorkflowResult
    {
        public EvaluationSubmitResponse Submit { get; set; }
        public EvaluationStatusResponse Status { get; set; }
        public EvaluationReportResponse Report { get; set; }
    }
}
