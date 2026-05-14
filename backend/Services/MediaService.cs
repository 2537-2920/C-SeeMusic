using backend.Data;
using backend.Models;

namespace backend.Services;

public class MediaService : IMediaService
{
    private readonly SeeMusicDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IBeatAnalysisService _beatAnalysisService;

    public MediaService(
        SeeMusicDbContext dbContext,
        IWebHostEnvironment environment,
        IBeatAnalysisService beatAnalysisService)
    {
        _dbContext = dbContext;
        _environment = environment;
        _beatAnalysisService = beatAnalysisService;
    }

    public async Task<MediaUploadResponse> UploadAsync(IFormFile file, string type, int? userId)
    {
        var uploadDirectory = Path.Combine(_environment.ContentRootPath, "uploads", "media");
        Directory.CreateDirectory(uploadDirectory);

        var fileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(fileName);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var relativeStoragePath = Path.Combine("media", storedName).Replace('\\', '/');
        var savePath = Path.Combine(uploadDirectory, storedName);

        await using var stream = File.Create(savePath);
        await file.CopyToAsync(stream);

        var mediaId = Guid.NewGuid().ToString("N");
        var media = new MediaFile
        {
            MediaId = mediaId,
            UserId = ResolveUserId(userId),
            FileName = fileName,
            Type = type,
            MimeType = ResolveMimeType(file, fileName),
            FileSize = file.Length,
            Url = $"/uploads/{relativeStoragePath}",
            StoragePath = relativeStoragePath,
            PreparedAudioStatus = "pending",
            CreatedAt = DateTime.UtcNow,
        };

        PopulatePreparedAudioMetadata(media, savePath);
        _dbContext.MediaFiles.Add(media);
        await _dbContext.SaveChangesAsync();

        return MapToUploadResponse(media);
    }

    public TranscriptionResult Analyze(TranscriptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MediaId))
        {
            return new TranscriptionResult
            {
                MediaId = string.Empty,
                Status = "failed",
                ScoreId = string.Empty,
                Message = "缺少音频标识，无法执行节拍分析。",
                BeatAnalysis = new BeatAnalysisResult
                {
                    Summary = "缺少音频标识，无法执行节拍分析。"
                }
            };
        }

        var mediaFile = _dbContext.MediaFiles.SingleOrDefault(item => item.MediaId == request.MediaId);
        if (mediaFile == null)
        {
            return new TranscriptionResult
            {
                MediaId = request.MediaId,
                Status = "failed",
                ScoreId = string.Empty,
                Message = "未找到对应的音频文件。",
                BeatAnalysis = new BeatAnalysisResult
                {
                    Summary = "未找到对应的音频文件。"
                }
            };
        }

        var filePath = ResolvePreparedFilePath(mediaFile);
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new TranscriptionResult
            {
                MediaId = request.MediaId,
                Status = "failed",
                ScoreId = string.Empty,
                Message = string.IsNullOrWhiteSpace(mediaFile.PreparationErrorMessage)
                    ? "当前仅支持直接分析 WAV 音频。"
                    : mediaFile.PreparationErrorMessage,
                BeatAnalysis = new BeatAnalysisResult
                {
                    Summary = string.IsNullOrWhiteSpace(mediaFile.PreparationErrorMessage)
                        ? "当前仅支持直接分析 WAV 音频。"
                        : mediaFile.PreparationErrorMessage
                }
            };
        }

        var beatAnalysis = _beatAnalysisService.AnalyzeFile(filePath);

        return new TranscriptionResult
        {
            MediaId = request.MediaId,
            Status = beatAnalysis.IsAvailable ? "ready" : "failed",
            ScoreId = Guid.NewGuid().ToString("N"),
            Message = beatAnalysis.IsAvailable
                ? "分析完成，已提取节拍与节奏特征。"
                : string.IsNullOrWhiteSpace(beatAnalysis.Summary)
                    ? "当前文件未能提取到可靠节拍。"
                    : beatAnalysis.Summary,
            BeatAnalysis = beatAnalysis
        };
    }

    private string? ResolvePreparedFilePath(MediaFile mediaFile)
    {
        var relativePath = !string.IsNullOrWhiteSpace(mediaFile.PreparedAudioPath)
            ? mediaFile.PreparedAudioPath
            : mediaFile.StoragePath;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var absolutePath = Path.Combine(
            _environment.ContentRootPath,
            "uploads",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        return absolutePath;
    }

    private void PopulatePreparedAudioMetadata(MediaFile media, string absolutePath)
    {
        if (!IsWaveFile(media.FileName, media.MimeType))
        {
            media.PreparedAudioStatus = "pending";
            return;
        }

        try
        {
            var audioData = WavAudioReader.Read(absolutePath);
            media.DurationMs = (int)Math.Round(audioData.DurationSeconds * 1000);
            media.PreparedAudioStatus = "ready";
            media.PreparedAudioPath = media.StoragePath;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            media.PreparedAudioStatus = "failed";
            media.PreparationErrorMessage = exception.Message;
        }
    }

    private static bool IsWaveFile(string fileName, string mimeType)
    {
        return string.Equals(Path.GetExtension(fileName), ".wav", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mimeType, "audio/wav", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mimeType, "audio/x-wav", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveMimeType(IFormFile file, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(file.ContentType)
            && !string.Equals(file.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return file.ContentType;
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            _ => "application/octet-stream"
        };
    }

    private static MediaUploadResponse MapToUploadResponse(MediaFile media)
    {
        return new MediaUploadResponse
        {
            MediaId = media.MediaId,
            FileName = media.FileName,
            Url = media.Url,
            Type = media.Type,
            MimeType = media.MimeType,
            FileSize = media.FileSize,
            DurationMs = media.DurationMs,
            PreparedAudioStatus = media.PreparedAudioStatus,
            PreparedAudioPath = string.IsNullOrWhiteSpace(media.PreparedAudioPath)
                ? null
                : $"/uploads/{media.PreparedAudioPath}"
        };
    }

    private int? ResolveUserId(int? requestedUserId)
    {
        if (requestedUserId is > 0 && _dbContext.Users.Any(user => user.Id == requestedUserId.Value))
        {
            return requestedUserId.Value;
        }

        return null;
    }
}
