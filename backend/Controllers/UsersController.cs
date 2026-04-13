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
    public ActionResult<ApiResponse<string>> UploadAvatar([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ApiResponse<string> { Code = 40001, Message = "file required" });

        return Ok(new ApiResponse<string> { Data = "avatar-uploaded" });
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
