using backend.Models;

namespace backend.Services;

public interface IMediaService
{
    Task<MediaUploadResponse> UploadAsync(IFormFile file, string type, int userId);
    TranscriptionResult Analyze(TranscriptionRequest request);
}
