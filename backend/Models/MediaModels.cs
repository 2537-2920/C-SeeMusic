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
}

public class TranscriptionRequest
{
    public string MediaId { get; set; } = string.Empty;
}

public class TranscriptionResult
{
    public string MediaId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ScoreId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class AvatarUploadRequest
{
    public IFormFile File { get; set; } = null!;
}
