using backend.Auth;
using backend.Data;
using backend.Models;
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

    public UserDto Register(string username, string email, string password)
    {
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

        return MapToUserDto(user);
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
        return MapToUserDto(user);
    }

    public UserDto UpdateProfile(int userId, UserDto profile)
    {
        var user = _dbContext.Users.Find(userId)
            ?? throw new InvalidOperationException("用户不存在");

        user.DisplayName = profile.DisplayName;
        user.Bio = profile.Bio;
        if (!string.IsNullOrEmpty(profile.AvatarUrl))
        {
            user.AvatarUrl = profile.AvatarUrl;
        }

        _dbContext.SaveChanges();
        return MapToUserDto(user);
    }

    private UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl ?? string.Empty,
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
