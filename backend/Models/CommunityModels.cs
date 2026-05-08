using backend.Models;

namespace backend.Models;

public class ScoreDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    public string? ArrangementTag { get; set; }
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public int PriceCent { get; set; }
    public int DownloadCount { get; set; }
    public int FavoriteCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CommentDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ScoreDetailDto : ScoreDto
{
    public List<CommentDto> RecentComments { get; set; } = new();
}

public class CreateCommentRequest
{
    public int ScoreId { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class CreateScoreRequest
{
    public string Title { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    public string? ArrangementTag { get; set; }
    public string? Description { get; set; }
    public int PriceCent { get; set; }
    public string? Category { get; set; }
}
