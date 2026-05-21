using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("register")]
    public ActionResult<ApiResponse<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var user = _userService.Register(request.Username, request.Email, request.Password, request.ConfirmPassword);
            return Ok(new ApiResponse<RegisterResponse> { Data = user });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<RegisterResponse> { Code = 40001, Message = ex.Message });
        }
    }

    [HttpPost("login")]
    public ActionResult<ApiResponse<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var auth = _userService.Login(request.Account, request.Password);
            return Ok(new ApiResponse<AuthResponse> { Data = auth });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<AuthResponse> { Code = 40001, Message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public ActionResult<ApiResponse<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var auth = _userService.RefreshToken(request.RefreshToken);
            return Ok(new ApiResponse<AuthResponse> { Data = auth });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<AuthResponse> { Code = 40001, Message = ex.Message });
        }
    }

    [HttpPost("logout")]
    public ActionResult<ApiResponse<string>> Logout()
    {
        try
        {
            var userIdClaim = User.FindFirst("userId");
            if (userIdClaim != null)
            {
                var userId = int.Parse(userIdClaim.Value);
                _userService.Logout(userId);
                return Ok(new ApiResponse<string> { Data = "登出成功" });
            }
            else
            {
                return BadRequest(new ApiResponse<string> { Code = 40001, Message = "未找到用户信息" });
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<string> { Code = 40001, Message = ex.Message });
        }
    }
}
