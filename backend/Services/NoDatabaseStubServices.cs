using backend.Models;

namespace backend.Services;

internal sealed class NoDatabaseMediaService : IMediaService
{
    private const string Message = "识谱功能需要数据库支持，请在 appsettings.json 中配置有效的数据库连接字符串。";

    public Task<MediaUploadResponse> UploadAsync(IFormFile file, string type, int? userId)
        => throw new InvalidOperationException(Message);

    public TranscriptionResult Analyze(TranscriptionRequest request)
        => throw new InvalidOperationException(Message);
}

internal sealed class NoDatabaseTranscriptionService : ITranscriptionService
{
    private const string Message = "识谱功能需要数据库支持，请在 appsettings.json 中配置有效的数据库连接字符串。";

    public Task<CreateTranscriptionResponse> CreateAsync(
        CreateTranscriptionRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(Message);

    public Task<TranscriptionStatusResponse> GetStatusAsync(
        string jobId,
        int? userId,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(Message);

    public Task<ScoreDetailResponse> GetScoreAsync(
        string scoreId,
        int? userId,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(Message);

    public Task<TranscriptionResult> AnalyzeLegacyAsync(
        TranscriptionRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(Message);

    public Task ProcessQueuedTranscriptionAsync(int transcriptionJobDbId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
