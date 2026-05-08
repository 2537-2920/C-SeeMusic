using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/community")]
public class CommunityController : ControllerBase
{
    private readonly SeeMusicDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public CommunityController(SeeMusicDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpPost("scores")]
    public async Task<ActionResult<ApiResponse<ScoreDto>>> UploadScore(
        [FromForm] CreateScoreRequest request, 
        [FromForm] IFormFile file,
        [FromForm] IFormFile? cover)
    {
        int userId = 1; // Prototype user

        // 处理乐谱文件
        string? fileUrl = null;
        if (file != null && file.Length > 0)
        {
            var folder = Path.Combine(_environment.WebRootPath, "uploads", "scores");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(folder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
            fileUrl = $"/uploads/scores/{fileName}";
        }

        // 处理封面文件
        string? coverUrl = null;
        if (cover != null && cover.Length > 0)
        {
            var folder = Path.Combine(_environment.WebRootPath, "uploads", "covers");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(cover.FileName);
            var filePath = Path.Combine(folder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create)) { await cover.CopyToAsync(stream); }
            coverUrl = $"/uploads/covers/{fileName}";
        }

        var score = new Score
        {
            Title = request.Title,
            ArtistName = request.ArtistName,
            ArrangementTag = request.ArrangementTag,
            Description = request.Description,
            PriceCent = request.PriceCent,
            PrimaryCategory = request.Category,
            OwnerUserId = userId,
            CoverUrl = coverUrl,
            IsPublic = true,
            Status = "published",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Scores.Add(score);
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<ScoreDto> 
        { 
            Data = new ScoreDto 
            {
                Id = score.Id,
                Title = score.Title,
                ArtistName = score.ArtistName,
                ArrangementTag = score.ArrangementTag,
                CoverUrl = score.CoverUrl,
                OwnerName = "当前用户",
                CreatedAt = score.CreatedAt
            }
        });
    }

    [HttpGet("scores")]
    public async Task<ActionResult<ApiResponse<List<ScoreDto>>>> GetScores([FromQuery] string? category = null)
    {
        // 1. 强制包含 Owner 关联
        var query = _context.Scores.Include(s => s.Owner).AsQueryable();

        // 2. 优化筛选逻辑：如果选的是“精选”或者没传分类，则显示全部
        if (!string.IsNullOrEmpty(category) && category != "精选" && category != "All")
        {
            query = query.Where(s => s.PrimaryCategory == category);
        }

        var scores = await query
            .Where(s => s.Status == "published") // 暂时移除 IsPublic 强制约束，优先保证能看到数据
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ScoreDto
            {
                Id = s.Id,
                Title = s.Title,
                ArtistName = s.ArtistName,
                ArrangementTag = s.ArrangementTag,
                Description = s.Description,
                PriceCent = s.PriceCent,
                DownloadCount = s.DownloadCount,
                FavoriteCount = s.FavoriteCount,
                CommentCount = s.CommentCount,
                ShareCount = s.ShareCount,
                CoverUrl = s.CoverUrl,
                // 3. 增强发布者名称展示逻辑
                OwnerName = s.Owner != null ? (string.IsNullOrEmpty(s.Owner.DisplayName) ? s.Owner.Username : s.Owner.DisplayName) : "官方发布",
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return Ok(new ApiResponse<List<ScoreDto>> { Data = scores });
    }

    [HttpGet("scores/{id}")]
    public async Task<ActionResult<ApiResponse<ScoreDetailDto>>> GetScoreDetail(int id)
    {
        var score = await _context.Scores
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (score == null)
            return NotFound(new ApiResponse<ScoreDetailDto> { Code = 404, Message = "乐谱未找到" });

        var comments = await _context.Comments
            .Where(c => c.ScoreId == id && c.Status == "visible")
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .Select(c => new CommentDto
            {
                Id = c.Id,
                Username = c.User != null ? c.User.DisplayName : "匿名用户",
                AvatarUrl = c.User != null ? c.User.AvatarUrl : null,
                Content = c.Content,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        var detail = new ScoreDetailDto
        {
            Id = score.Id,
            Title = score.Title,
            ArtistName = score.ArtistName,
            ArrangementTag = score.ArrangementTag,
            Description = score.Description,
            PriceCent = score.PriceCent,
            DownloadCount = score.DownloadCount,
            FavoriteCount = score.FavoriteCount,
            CommentCount = score.CommentCount,
            OwnerName = score.Owner != null ? score.Owner.DisplayName : "未知用户",
            CreatedAt = score.CreatedAt,
            RecentComments = comments
        };

        return Ok(new ApiResponse<ScoreDetailDto> { Data = detail });
    }

    [HttpPost("comments")]
    public async Task<ActionResult<ApiResponse<CommentDto>>> AddComment([FromBody] CreateCommentRequest request)
    {
        // For prototype simplicity, we'll use a hardcoded user if not authenticated
        // In real app, get from User.Identity
        int userId = 1; 

        var comment = new Comment
        {
            ScoreId = request.ScoreId,
            UserId = userId,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow,
            Status = "visible"
        };

        _context.Comments.Add(comment);
        
        var score = await _context.Scores.FindAsync(request.ScoreId);
        if (score != null) score.CommentCount++;

        await _context.SaveChangesAsync();

        var user = await _context.Users.FindAsync(userId);

        return Ok(new ApiResponse<CommentDto> 
        { 
            Data = new CommentDto 
            {
                Id = comment.Id,
                Username = user?.DisplayName ?? "匿名用户",
                AvatarUrl = user?.AvatarUrl,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt
            }
        });
    }

    [HttpPost("favorite/{scoreId}")]
    public async Task<ActionResult<ApiResponse<bool>>> ToggleFavorite(int scoreId)
    {
        int userId = 1; // Prototype user

        var existing = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ScoreId == scoreId);

        var score = await _context.Scores.FindAsync(scoreId);
        if (score == null) return NotFound();

        bool isFavorite;
        if (existing != null)
        {
            _context.Favorites.Remove(existing);
            score.FavoriteCount--;
            isFavorite = false;
        }
        else
        {
            _context.Favorites.Add(new Favorite { UserId = userId, ScoreId = scoreId });
            score.FavoriteCount++;
            isFavorite = true;
        }

        await _context.SaveChangesAsync();
        return Ok(new ApiResponse<bool> { Data = isFavorite });
    }
}
