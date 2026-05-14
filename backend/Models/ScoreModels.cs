using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public class Score
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ArtistName { get; set; }

    [MaxLength(50)]
    public string? ArrangementTag { get; set; }

    public string? Description { get; set; }

    public int OwnerUserId { get; set; }

    [ForeignKey("OwnerUserId")]
    public User? Owner { get; set; }

    public string? CoverUrl { get; set; }

    public string? FileUrl { get; set; }

    public int PriceCent { get; set; } // 以分为单位，0 为免费

    public bool IsPublic { get; set; } = true;

    public int DownloadCount { get; set; } = 0;

    public int FavoriteCount { get; set; } = 0;

    public int CommentCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 关联属性
    public ICollection<ScoreCategoryRelation> CategoryRelations { get; set; } = new List<ScoreCategoryRelation>();
    public ICollection<ScoreComment> Comments { get; set; } = new List<ScoreComment>();
}

public class ScoreCategory
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;
}

public class ScoreCategoryRelation
{
    public int ScoreId { get; set; }
    public Score Score { get; set; } = null!;

    public int CategoryId { get; set; }
    public ScoreCategory Category { get; set; } = null!;
}
