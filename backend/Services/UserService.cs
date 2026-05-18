using backend.Auth;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace backend.Services;

public class UserService : IUserService
{
    private readonly SeeMusicDbContext _dbContext;
    private readonly JwtTokenProvider _tokenProvider;

    public UserService(SeeMusicDbContext dbContext, JwtTokenProvider tokenProvider)
    {
        _dbContext = dbContext;
        _tokenProvider = tokenProvider;
    }

    public RegisterResponse Register(string username, string email, string password, string confirmPassword)
    {
        if (password != confirmPassword)
        {
            throw new InvalidOperationException("两次输入的密码不一致");
        }

        var existingUser = _dbContext.Users.FirstOrDefault(u => u.Username == username || u.Email == email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("用户名或邮箱已存在");
        }

        var passwordHash = HashPassword(password);
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            DisplayName = username,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
        };

        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();

        return new RegisterResponse
        {
            UserId = user.Id,
            Username = user.Username,
        };
    }

    public AuthResponse Login(string account, string password)
    {
        var user = _dbContext.Users.FirstOrDefault(u => u.Username == account || u.Email == account);
        if (user == null || !VerifyPassword(password, user.PasswordHash))
        {
            throw new InvalidOperationException("用户名或密码错误");
        }

        user.LastLoginAt = DateTime.UtcNow;
        _dbContext.SaveChanges();

        var accessToken = _tokenProvider.GenerateAccessToken(user.Id, user.Username);
        var refreshToken = _tokenProvider.GenerateRefreshToken();

        var tokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        _dbContext.RefreshTokens.Add(tokenEntity);
        _dbContext.SaveChanges();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 7200,
            User = MapToUserDto(user),
        };
    }

    public AuthResponse RefreshToken(string refreshToken)
    {
        var tokenEntity = _dbContext.RefreshTokens.FirstOrDefault(rt => rt.Token == refreshToken && rt.ExpiresAt > DateTime.UtcNow);
        if (tokenEntity == null)
        {
            throw new InvalidOperationException("刷新令牌已过期或无效");
        }

        var user = _dbContext.Users.Find(tokenEntity.UserId);
        if (user == null)
        {
            throw new InvalidOperationException("用户不存在");
        }

        _dbContext.RefreshTokens.Remove(tokenEntity);
        _dbContext.SaveChanges();

        var accessToken = _tokenProvider.GenerateAccessToken(user.Id, user.Username);
        var newRefreshToken = _tokenProvider.GenerateRefreshToken();

        var newTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        _dbContext.RefreshTokens.Add(newTokenEntity);
        _dbContext.SaveChanges();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = 7200,
            User = MapToUserDto(user),
        };
    }

    public UserDto GetCurrentUser(int userId)
    {
        var user = _dbContext.Users.Find(userId)
            ?? throw new InvalidOperationException("用户不存在");
        
        var dto = MapToUserDto(user);
        
        // Calculate dynamic stats
        dto.TranscriptionCount = _dbContext.Scores.Count(s => s.OwnerUserId == userId);
        dto.FavoriteCount = _dbContext.ScoreFavorites.Count(f => f.UserId == userId);
        dto.EvaluationDurationHours = 12; // Placeholder for now or calculate from some future session table
        
        return dto;
    }

    public UserDto UpdateProfile(int userId, UserDto profile)
    {
        var user = _dbContext.Users.Find(userId)
            ?? throw new InvalidOperationException("用户不存在");

        user.DisplayName = profile.DisplayName;
        user.Email = profile.Email;
        user.Bio = profile.Bio;
        if (!string.IsNullOrEmpty(profile.AvatarUrl))
        {
            user.AvatarUrl = profile.AvatarUrl;
        }

        _dbContext.SaveChanges();
        
        return GetCurrentUser(userId);
    }

    public async Task<DashboardResponse> GetDashboardAsync(int userId)
    {
        var user = await _dbContext.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("用户不存在");

        var transcriptionCount = await _dbContext.Scores.CountAsync(s => s.OwnerUserId == userId);
        var favoriteCount = await _dbContext.ScoreFavorites.CountAsync(f => f.UserId == userId);

        // 计算本周每天的乐谱上传数量
        var now = DateTime.UtcNow;
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek).Date; // 本周日 00:00:00
        
        var weeklyUsage = new List<WeeklyUsageItem>();
        var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        
        // 获取用户所有乐谱的创建时间
        var allScores = await _dbContext.Scores
            .Where(s => s.OwnerUserId == userId)
            .Select(s => s.CreatedAt)
            .ToListAsync();
        
        for (int i = 0; i < 7; i++)
        {
            var dayStart = startOfWeek.AddDays(i);
            var dayEnd = dayStart.AddDays(1);
            
            var count = allScores.Count(c => c >= dayStart && c < dayEnd);
            
            weeklyUsage.Add(new WeeklyUsageItem
            {
                Day = dayNames[i],
                Value = count
            });
        }

        return new DashboardResponse
        {
            Profile = new DashboardProfile
            {
                DisplayName = user.DisplayName,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl ?? $"https://api.dicebear.com/7.x/avataaars/svg?seed={user.Username}"
            },
            Stats = new DashboardStats
            {
                TranscriptionCount = transcriptionCount,
                EvaluationDurationHours = 0,
                FavoriteCount = favoriteCount
            },
            WeeklyUsage = weeklyUsage
        };
    }

    public async Task<UserPreferencesDto> GetPreferencesAsync(int userId)
    {
        var user = await _dbContext.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("用户不存在");

        var prefs = await _dbContext.UserPreferences.FindAsync(userId);
        
        if (prefs == null)
        {
            return new UserPreferencesDto();
        }

        return new UserPreferencesDto
        {
            Theme = prefs.Theme,
            DefaultExportFormats = prefs.DefaultExportFormats.Split(',').ToList(),
            SyncPreferences = prefs.SyncEnabled
        };
    }

    public async Task<UserPreferencesDto> UpdatePreferencesAsync(int userId, UpdatePreferencesRequest request)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new InvalidOperationException("用户不存在");

        var prefs = await _dbContext.UserPreferences.FindAsync(userId);

        if (prefs == null)
        {
            prefs = new UserPreferences { UserId = userId };
            _dbContext.UserPreferences.Add(prefs);
        }

        prefs.Theme = request.Theme;
        prefs.DefaultExportFormats = string.Join(",", request.DefaultExportFormats);
        prefs.SyncEnabled = request.SyncPreferences;
        
        _dbContext.SaveChanges();

        return new UserPreferencesDto
        {
            Theme = prefs.Theme,
            DefaultExportFormats = prefs.DefaultExportFormats.Split(',').ToList(),
            SyncPreferences = prefs.SyncEnabled
        };
    }

    public async Task<string> UploadAvatarAsync(int userId, IFormFile file)
    {
        var user = await _dbContext.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("用户不存在");

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "avatars");
        Directory.CreateDirectory(uploadsDir);

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            throw new InvalidOperationException("不支持的文件格式");
        }

        var fileName = $"{userId}_{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        user.AvatarUrl = $"/uploads/avatars/{fileName}";
        await _dbContext.SaveChangesAsync();

        return user.AvatarUrl;
    }

    public void ChangePassword(int userId, string currentPassword, string newPassword)
    {
        var user = _dbContext.Users.Find(userId)
            ?? throw new InvalidOperationException("用户不存在");

        if (!VerifyPassword(currentPassword, user.PasswordHash))
        {
            throw new InvalidOperationException("当前密码不正确");
        }

        user.PasswordHash = HashPassword(newPassword);
        _dbContext.SaveChanges();
    }

    private UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl ?? "https://api.dicebear.com/7.x/avataaars/svg?seed=" + user.Username,
            Bio = user.Bio,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
        };
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private bool VerifyPassword(string password, string hash)
    {
        var hashOfInput = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password)));
        return hashOfInput.Equals(hash);
    }
}