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
    public ActionResult<ApiResponse<UserDto>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var user = _userService.Register(request.Username, request.Email, request.Password);
            return Ok(new ApiResponse<UserDto> { Data = user });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<UserDto> { Code = 40001, Message = ex.Message });
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
        return Ok(new ApiResponse<string> { Data = "ok" });
    }
}
