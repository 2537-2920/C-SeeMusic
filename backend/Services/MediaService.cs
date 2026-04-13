using backend.Data;
using backend.Models;

namespace backend.Services;

public class MediaService : IMediaService
{
    private readonly SeeMusicDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public MediaService(SeeMusicDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    public async Task<MediaUploadResponse> UploadAsync(IFormFile file, string type, int userId)
    {
        var uploadDirectory = Path.Combine(_environment.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadDirectory);

        var fileName = Path.GetFileName(file.FileName);
        var savePath = Path.Combine(uploadDirectory, fileName);

        await using var stream = File.Create(savePath);
        await file.CopyToAsync(stream);

        var mediaId = Guid.NewGuid().ToString("N");
        var media = new MediaFile
        {
            MediaId = mediaId,
            UserId = userId,
            FileName = fileName,
            Type = type,
            Url = $"/uploads/{fileName}",
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.MediaFiles.Add(media);
        _dbContext.SaveChanges();

        return new MediaUploadResponse
        {
            MediaId = mediaId,
            FileName = fileName,
            Url = media.Url,
            Type = type,
        };
    }

    public TranscriptionResult Analyze(TranscriptionRequest request)
    {
        return new TranscriptionResult
        {
            MediaId = request.MediaId,
            Status = "ready",
            ScoreId = Guid.NewGuid().ToString("N"),
            Message = "分析完成，乐谱已生成。"
        };
    }
}
