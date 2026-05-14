using backend.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Services;

public interface ICommunityService
{
    Task<List<ScoreDto>> GetScoresAsync(string? keyword, string? category, string? sort, int page, int pageSize);
    Task<ScoreDetailDto?> GetScoreDetailAsync(int scoreId, int? userId = null);
    Task<List<CommentDto>> GetCommentsAsync(int scoreId);
    Task<bool> AddCommentAsync(int scoreId, int userId, string content);
    Task<bool> ToggleFavoriteAsync(int scoreId, int userId, bool favorite);
    Task<string?> GetDownloadUrlAsync(int scoreId, int userId);
    Task<ScoreDto> UploadScoreAsync(ScoreUploadRequest request, IFormFile scoreFile, IFormFile? coverFile, int userId);
    Task<Dictionary<string, int>> GetCategoryStatsAsync();
}
