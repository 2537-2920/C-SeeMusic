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
    public int? UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public string Url { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public int? DurationMs { get; set; }
    public string PreparedAudioStatus { get; set; } = "pending";
    public string? PreparedAudioPath { get; set; }
    public string? PreparationErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Score
{
    public int Id { get; set; }
    public string ScoreId { get; set; } = Guid.NewGuid().ToString("N");
    public int? UserId { get; set; }
    public int OwnerUserId { get; set; }
    public int SourceMediaFileId { get; set; }
    public int? CoverMediaFileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    public string? ArrangementTag { get; set; }
    public string? Description { get; set; }
    public string InstrumentMode { get; set; } = "piano";
    public string Status { get; set; } = "published";
    public string? SourceType { get; set; } = "audio";
    public bool IsPublic { get; set; } = true;
    public int PriceCent { get; set; } = 0;
    public int DownloadCount { get; set; } = 0;
    public int FavoriteCount { get; set; } = 0;
    public int CommentCount { get; set; } = 0;
    public double? TempoBpm { get; set; }
    public string TimeSignature { get; set; } = "4/4";
    public string KeySignature { get; set; } = "C";
    public int MeasureCount { get; set; } = 0;
    public int EstimatedPageCount { get; set; } = 0;
    public string MusicXmlContent { get; set; } = "{}";
    public string AnalysisSummaryJson { get; set; } = "{}";
    public string WarningMessagesJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    // Navigation properties
    public User? Owner { get; set; }
    public MediaFile? CoverMediaFile { get; set; }
    public MediaFile? SourceMediaFile { get; set; }
    public ICollection<ScoreCategoryRelation> CategoryRelations { get; set; } = new List<ScoreCategoryRelation>();
    public ICollection<ScoreComment> Comments { get; set; } = new List<ScoreComment>();
}

public sealed class ScoreCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}

public sealed class ScoreCategoryRelation
{
    public int ScoreDbId { get; set; }
    public int CategoryId { get; set; }
    public Score? Score { get; set; }
    public ScoreCategory? Category { get; set; }
}

public sealed class ScoreComment
{
    public int Id { get; set; }
    public int ScoreDbId { get; set; }
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
    public int ScoreDbId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ScoreDownload
{
    public int Id { get; set; }
    public int ScoreDbId { get; set; }
    public int UserId { get; set; }
    public string? SourceIp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
