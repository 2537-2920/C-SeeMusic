using System;
using System.Collections.Generic;

namespace backend.Models;

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Bio { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = string.Empty;
}

public sealed class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class MediaFile
{
    public int Id { get; set; }
    public string MediaId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Score
{
    public int Id { get; set; }
    public int OwnerUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    public string? ArrangementTag { get; set; }
    public string? Description { get; set; }
    public int? SourceMediaId { get; set; }
    public int? CoverMediaId { get; set; }
    public string? KeySignature { get; set; }
    public string? TimeSignature { get; set; }
    public int? Tempo { get; set; }
    public string Status { get; set; } = "published"; // draft, processing, ready, published
    public string SourceType { get; set; } = "audio"; // audio, video, microphone, sample
    public bool IsPublic { get; set; } = true;
    public int PriceCent { get; set; } = 0;
    public int DownloadCount { get; set; } = 0;
    public int FavoriteCount { get; set; } = 0;
    public int CommentCount { get; set; } = 0;
    public int ShareCount { get; set; } = 0;
    public string? CoverUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? PrimaryCategory { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    // Navigation properties
    public User? Owner { get; set; }
    public ICollection<ScoreCategoryRelation> CategoryRelations { get; set; } = new List<ScoreCategoryRelation>();
    public ICollection<ScoreComment> Comments { get; set; } = new List<ScoreComment>();
}

public sealed class ScoreCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}

public sealed class ScoreCategoryRelation
{
    public int ScoreId { get; set; }
    public int CategoryId { get; set; }
    public Score? Score { get; set; }
    public ScoreCategory? Category { get; set; }
}

public sealed class ScoreComment
{
    public int Id { get; set; }
    public int ScoreId { get; set; }
    public int UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "visible";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
}

public sealed class ScoreFavorite
{
    public int UserId { get; set; }
    public int ScoreId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ScoreDownload
{
    public int Id { get; set; }
    public int ScoreId { get; set; }
    public int UserId { get; set; }
    public string? IPAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
