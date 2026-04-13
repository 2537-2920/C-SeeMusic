namespace backend.Models;

public sealed class MediaUploadResponse
{
    public string MediaId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
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
}
