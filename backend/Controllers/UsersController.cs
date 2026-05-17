using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public ActionResult<ApiResponse<UserDto>> GetCurrentUser()
    {
        var userId = GetCurrentUserId();
        var user = _userService.GetCurrentUser(userId);
        return Ok(new ApiResponse<UserDto> { Data = user });
    }

    [HttpPut("me")]
    public ActionResult<ApiResponse<UserDto>> UpdateProfile([FromBody] UserDto profile)
    {
        var userId = GetCurrentUserId();
        var updated = _userService.UpdateProfile(userId, profile);
        return Ok(new ApiResponse<UserDto> { Data = updated });
    }

    [HttpPost("me/avatar")]
    public ActionResult<ApiResponse<string>> UploadAvatar([FromForm] AvatarUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest(new ApiResponse<string> { Code = 40001, Message = "file required" });

        return Ok(new ApiResponse<string> { Data = "avatar-uploaded" });
    }

    [HttpGet("me/dashboard")]
    public async Task<ActionResult<ApiResponse<DashboardResponse>>> GetDashboard()
    {
        var userId = GetCurrentUserId();
        var dashboard = await _userService.GetDashboardAsync(userId);
        return Ok(new ApiResponse<DashboardResponse> { Data = dashboard });
    }

    [HttpGet("me/preferences")]
    public async Task<ActionResult<ApiResponse<UserPreferencesDto>>> GetPreferences()
    {
        var userId = GetCurrentUserId();
        var preferences = await _userService.GetPreferencesAsync(userId);
        return Ok(new ApiResponse<UserPreferencesDto> { Data = preferences });
    }

    [HttpPut("me/preferences")]
    public async Task<ActionResult<ApiResponse<UserPreferencesDto>>> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
    {
        var userId = GetCurrentUserId();
        var preferences = await _userService.UpdatePreferencesAsync(userId, request);
        return Ok(new ApiResponse<UserPreferencesDto> { Data = preferences });
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            throw new InvalidOperationException("Invalid user");
        }
        return userId;
    }
}
