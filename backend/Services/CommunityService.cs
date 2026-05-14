using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace backend.Services;

public class CommunityService : ICommunityService
{
    private readonly SeeMusicDbContext _context;

    public CommunityService(SeeMusicDbContext context)
    {
        _context = context;
    }

    public async Task<List<ScoreDto>> GetScoresAsync(string? keyword, string? category, string? sort, int page, int pageSize)
    {
        var query = _context.Scores.AsQueryable();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(s => s.Title.Contains(keyword) || (s.ArtistName != null && s.ArtistName.Contains(keyword)));
        }

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(s => s.CategoryRelations.Any(cr => cr.Category.Name == category));
        }

        // 排序逻辑
        query = sort switch
        {
            "latest" => query.OrderByDescending(s => s.CreatedAt),
            "popular" => query.OrderByDescending(s => s.FavoriteCount),
            "downloads" => query.OrderByDescending(s => s.DownloadCount),
            _ => query.OrderByDescending(s => s.CreatedAt)
        };

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ScoreDto
            {
                Id = s.Id,
                Title = s.Title,
                AuthorName = s.ArtistName,
                ArrangementTag = s.ArrangementTag,
                CoverUrl = s.CoverUrl,
                Price = s.PriceCent,
                DownloadCount = s.DownloadCount,
                FavoriteCount = s.FavoriteCount
            })
            .ToListAsync();
    }

    public async Task<ScoreDetailDto?> GetScoreDetailAsync(int scoreId, int? userId = null)
    {
        var score = await _context.Scores
            .Include(s => s.Comments.OrderByDescending(c => c.CreatedAt).Take(5))
            .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(s => s.Id == scoreId);

        if (score == null) return null;

        var isFavorited = false;
        if (userId.HasValue)
        {
            // 使用更显式的 Count 方式查询，防止某些驱动下的 AnyAsync 优化问题
            isFavorited = await _context.ScoreFavorites
                .CountAsync(f => f.ScoreId == scoreId && f.UserId == userId.Value) > 0;
        }

        return new ScoreDetailDto
        {
            Id = score.Id,
            Title = score.Title,
            AuthorName = score.ArtistName,
            ArrangementTag = score.ArrangementTag,
            CoverUrl = score.CoverUrl,
            Price = score.PriceCent,
            DownloadCount = score.DownloadCount,
            FavoriteCount = score.FavoriteCount,
            Description = score.Description,
            FileUrl = score.FileUrl,
            CommentCount = score.CommentCount,
            IsFavorited = isFavorited,
            RecentComments = score.Comments.Select(c => new CommentDto
            {
                Id = c.Id,
                UserName = c.User?.DisplayName ?? c.User?.Username ?? "Unknown",
                Content = c.Content,
                CreatedAt = c.CreatedAt
            }).ToList()
        };
    }

    public async Task<List<CommentDto>> GetCommentsAsync(int scoreId)
    {
        return await _context.ScoreComments
            .Where(c => c.ScoreId == scoreId && c.Status == "visible")
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentDto
            {
                Id = c.Id,
                UserName = c.User.DisplayName ?? c.User.Username,
                Content = c.Content,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<bool> AddCommentAsync(int scoreId, int userId, string content)
    {
        var score = await _context.Scores.FindAsync(scoreId);
        if (score == null) return false;

        var comment = new ScoreComment
        {
            ScoreId = scoreId,
            UserId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            Status = "visible"
        };

        _context.ScoreComments.Add(comment);
        score.CommentCount++;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleFavoriteAsync(int scoreId, int userId, bool favorite)
    {
        var score = await _context.Scores.FindAsync(scoreId);
        if (score == null) return false;

        var existing = await _context.ScoreFavorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ScoreId == scoreId);

        if (favorite)
        {
            if (existing == null)
            {
                _context.ScoreFavorites.Add(new ScoreFavorite { UserId = userId, ScoreId = scoreId });
                score.FavoriteCount++;
            }
        }
        else
        {
            if (existing != null)
            {
                _context.ScoreFavorites.Remove(existing);
                score.FavoriteCount--;
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetDownloadUrlAsync(int scoreId, int userId)
    {
        var score = await _context.Scores.FindAsync(scoreId);
        if (score == null) return null;

        // 这里可以增加付费校验逻辑
        
        var download = new ScoreDownload
        {
            ScoreId = scoreId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.ScoreDownloads.Add(download);
        score.DownloadCount++;
        await _context.SaveChangesAsync();

        return score.FileUrl;
    }

    public async Task<ScoreDto> UploadScoreAsync(ScoreUploadRequest request, IFormFile scoreFile, IFormFile? coverFile, int userId)
    {
        string? coverUrl = null;
        if (coverFile != null)
        {
            coverUrl = await SaveFileAsync(coverFile, "covers");
        }

        string scoreUrl = await SaveFileAsync(scoreFile, "scores");

        var score = new Score
        {
            Title = request.Title,
            ArtistName = request.ArtistName,
            ArrangementTag = request.ArrangementTag,
            Description = request.Description,
            PriceCent = request.Price,
            OwnerUserId = userId,
            CoverUrl = coverUrl,
            FileUrl = scoreUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsPublic = true
        };

        _context.Scores.Add(score);
        await _context.SaveChangesAsync();

        // 处理分类
        if (!string.IsNullOrEmpty(request.Category))
        {
            var category = await _context.ScoreCategories.FirstOrDefaultAsync(c => c.Name == request.Category);
            if (category != null)
            {
                _context.ScoreCategoryRelations.Add(new ScoreCategoryRelation
                {
                    ScoreId = score.Id,
                    CategoryId = category.Id
                });
                await _context.SaveChangesAsync();
            }
        }

        return new ScoreDto
        {
            Id = score.Id,
            Title = score.Title,
            AuthorName = score.ArtistName,
            ArrangementTag = score.ArrangementTag,
            CoverUrl = score.CoverUrl,
            Price = score.PriceCent,
            DownloadCount = score.DownloadCount,
            FavoriteCount = score.FavoriteCount
        };
    }

    private async Task<string> SaveFileAsync(IFormFile file, string folder)
    {
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder);
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/{folder}/{fileName}";
    }
}
