using backend.Models;

namespace backend.Services;

public interface IUserService
{
    UserDto Register(string username, string email, string password);
    AuthResponse Login(string account, string password);
    AuthResponse RefreshToken(string refreshToken);
    UserDto GetCurrentUser(int userId);
    UserDto UpdateProfile(int userId, UserDto profile);
}
