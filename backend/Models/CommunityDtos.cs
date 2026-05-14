using System;
using System.Collections.Generic;

namespace backend.Models;

public class ScoreDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public string? ArrangementTag { get; set; }
    public string? CoverUrl { get; set; }
    public int Price { get; set; }
    public bool IsFree => Price == 0;
    public int DownloadCount { get; set; }
    public int FavoriteCount { get; set; }
}

public class ScoreDetailDto : ScoreDto
{
    public string? Description { get; set; }
    public string? FileUrl { get; set; }
    public int CommentCount { get; set; }
    public bool IsFavorited { get; set; }
    public List<CommentDto> RecentComments { get; set; } = new();
}

public class CommentDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ScoreUploadRequest
{
    public string Title { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    public string? ArrangementTag { get; set; }
    public string? Category { get; set; }
    public int Price { get; set; }
    public string? Description { get; set; }
    // 文件字段将在 Controller 中通过 IFormFile 处理
}

public class CommentRequest
{
    public string Content { get; set; } = string.Empty;
}
