namespace backend.Models;

public sealed class HealthStatusResponse
{
    public string Status { get; set; } = "ok";
    public bool ServiceAvailable { get; set; } = true;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CreateTranscriptionRequest
{
    public string SourceType { get; set; } = "audio";
    public string MediaId { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public TranscriptionOptionsRequest Options { get; set; } = new();
}

public sealed class TranscriptionOptionsRequest
{
    public string Mode { get; set; } = "piano";
    public bool SeparateMelody { get; set; } = true;
    public bool SeparateAccompaniment { get; set; } = true;
    public bool AnalyzeRhythm { get; set; } = true;

    public TranscriptionOptionsRequest Normalize()
    {
        return new TranscriptionOptionsRequest
        {
            Mode = string.IsNullOrWhiteSpace(Mode) ? "piano" : Mode.Trim().ToLowerInvariant(),
            SeparateMelody = SeparateMelody,
            SeparateAccompaniment = SeparateAccompaniment,
            AnalyzeRhythm = AnalyzeRhythm,
        };
    }
}

public sealed class CreateTranscriptionResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public int Progress { get; set; }
    public string? ScoreId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class TranscriptionStatusResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ScoreId { get; set; }
    public double? DetectedTempoBpm { get; set; }
    public string? DetectedTimeSignature { get; set; }
    public double? DetectedTimeSignatureConfidence { get; set; }
    public int? MeasureCount { get; set; }
    public int? EstimatedPageCount { get; set; }
    public string TrackBuildMode { get; set; } = string.Empty;
    public string RhythmGridSource { get; set; } = string.Empty;
    public List<ScoreTrackResponse> TrackSummaries { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class ScoreDetailResponse
{
    public string ScoreId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string InstrumentMode { get; set; } = "piano";
    public string Status { get; set; } = "ready";
    public double? TempoBpm { get; set; }
    public string TimeSignature { get; set; } = "4/4";
    public string KeySignature { get; set; } = "C";
    public double? KeyConfidence { get; set; }
    public double? TimeSignatureConfidence { get; set; }
    public int MeasureCount { get; set; }
    public int EstimatedPageCount { get; set; }
    public string MusicXmlContent { get; set; } = string.Empty;
    public string PreviewRenderMode { get; set; } = string.Empty;
    public string TrackBuildMode { get; set; } = string.Empty;
    public string RhythmGridSource { get; set; } = string.Empty;
    public List<ScoreTrackResponse> Tracks { get; set; } = new();
    public ScoreAnalysisSummaryResponse AnalysisSummary { get; set; } = new();
    public List<ScorePreviewPageResponse> PreviewPages { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class ScoreTrackResponse
{
    public string Name { get; set; } = string.Empty;
    public string HandRole { get; set; } = string.Empty;
    public string Instrument { get; set; } = "piano";
    public int NoteCount { get; set; }
    public int? RangeLowMidi { get; set; }
    public int? RangeHighMidi { get; set; }
    public bool IsGenerated { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class ScoreAnalysisSummaryResponse
{
    public string MelodySummary { get; set; } = string.Empty;
    public string AccompanimentSummary { get; set; } = string.Empty;
    public string AssignmentSummary { get; set; } = string.Empty;
    public string TrackBuildSummary { get; set; } = string.Empty;
    public double? KeyConfidence { get; set; }
    public string RhythmSummary { get; set; } = string.Empty;
}

public sealed class ScorePreviewPageResponse
{
    public int PageNumber { get; set; }
    public string SvgContent { get; set; } = string.Empty;
}
