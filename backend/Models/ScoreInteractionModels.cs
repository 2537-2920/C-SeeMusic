using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public class ScoreComment
{
    [Key]
    public int Id { get; set; }

    public int ScoreId { get; set; }
    
    [ForeignKey("ScoreId")]
    public Score Score { get; set; } = null!;

    public int UserId { get; set; }
    
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [Required]
    [MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "visible"; // visible, hidden, deleted
}

public class ScoreFavorite
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ScoreId { get; set; }
    public Score Score { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ScoreDownload
{
    [Key]
    public int Id { get; set; }

    public int ScoreId { get; set; }
    public int? UserId { get; set; } // 允许匿名下载（如果是免费资源）

    public string? IPAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
