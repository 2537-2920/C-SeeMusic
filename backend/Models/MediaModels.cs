using Microsoft.AspNetCore.Http;

namespace backend.Models;

public class MediaUploadRequest
{
    public IFormFile File { get; set; } = null!;
    public string Type { get; set; } = string.Empty;
}

public class MediaUploadResponse
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

public class TranscriptionRequest
{
    public string MediaId { get; set; } = string.Empty;
    public bool SeparateMelody { get; set; } = true;
    public bool SeparateAccompaniment { get; set; } = true;
}

public class TranscriptionResult
{
    public string MediaId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
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

public class AvatarUploadRequest
{
    public IFormFile File { get; set; } = null!;
}
