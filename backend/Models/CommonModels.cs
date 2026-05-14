namespace backend.Models;

public class ApiResponse<T>
{
    public int Code { get; set; } = 0;
    public string Message { get; set; } = "success";
    public T? Data { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Bio { get; set; } = string.Empty;
    public int TranscriptionCount { get; set; }
    public int EvaluationDurationHours { get; set; }
    public int FavoriteCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}
