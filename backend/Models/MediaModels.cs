namespace backend.Models;

public sealed class MediaUploadResponse
{
    public string MediaId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public int? DurationMs { get; set; }
    public string PreparedAudioStatus { get; set; } = "pending";
    public string? PreparedAudioPath { get; set; }
}

public sealed class TranscriptionRequest
{
    public string MediaId { get; set; } = string.Empty;
    public bool SeparateMelody { get; set; }
    public bool SeparateAccompaniment { get; set; }
}

public sealed class TranscriptionResult
{
    public string MediaId { get; set; } = string.Empty;
    public string Status { get; set; } = "processing";
    public string ScoreId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public BeatAnalysisResult BeatAnalysis { get; set; } = new();
}

public sealed class BeatAnalysisResult
{
    public bool IsAvailable { get; set; }
    public double TempoBpm { get; set; }
    public List<double> BeatTimes { get; set; } = new();
    public double Stability { get; set; }
    public double Confidence { get; set; }
    public int TimeSignatureNumerator { get; set; } = 4;
    public int TimeSignatureDenominator { get; set; } = 4;
    public double TimeSignatureConfidence { get; set; }
    public string GridSource { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
