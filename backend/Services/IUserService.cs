using backend.Models;

namespace backend.Services;

public interface IUserService
{
    RegisterResponse Register(string username, string email, string password, string confirmPassword);
    AuthResponse Login(string account, string password);
    AuthResponse RefreshToken(string refreshToken);
    UserDto GetCurrentUser(int userId);
    UserDto UpdateProfile(int userId, UserDto profile);
    Task<DashboardResponse> GetDashboardAsync(int userId);
    Task<UserPreferencesDto> GetPreferencesAsync(int userId);
    Task<UserPreferencesDto> UpdatePreferencesAsync(int userId, UpdatePreferencesRequest request);
    Task<string> UploadAvatarAsync(int userId, IFormFile file);
}
