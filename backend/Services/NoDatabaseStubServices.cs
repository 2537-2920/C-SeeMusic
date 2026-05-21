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

internal sealed class NoDatabaseCommunityService : ICommunityService
{
    private static readonly Task<List<ScoreDto>> EmptyScores = Task.FromResult(new List<ScoreDto>());
    private static readonly Task<List<CommentDto>> EmptyComments = Task.FromResult(new List<CommentDto>());
    private static readonly Task<Dictionary<string, int>> EmptyStats = Task.FromResult(new Dictionary<string, int>());

    public Task<List<ScoreDto>> GetScoresAsync(string? keyword, string? category, string? sort, int page, int pageSize) => EmptyScores;
    public Task<ScoreDetailDto?> GetScoreDetailAsync(int scoreId, int? userId = null) => Task.FromResult<ScoreDetailDto?>(null);
    public Task<List<CommentDto>> GetCommentsAsync(int scoreId) => EmptyComments;
    public Task<bool> AddCommentAsync(int scoreId, int userId, string content) => Task.FromResult(false);
    public Task<bool> ToggleFavoriteAsync(int scoreId, int userId, bool favorite) => Task.FromResult(false);
    public Task<string?> GetDownloadUrlAsync(int scoreId, int userId) => Task.FromResult<string?>(null);
    public Task<ScoreDto> UploadScoreAsync(ScoreUploadRequest request, IFormFile scoreFile, IFormFile? coverFile, int userId)
        => throw new InvalidOperationException("社区功能需要数据库支持，请在 appsettings.json 中配置有效的数据库连接字符串。");
    public Task<Dictionary<string, int>> GetCategoryStatsAsync() => EmptyStats;
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
