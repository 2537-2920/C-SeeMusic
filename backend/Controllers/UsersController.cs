using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    private readonly IWebHostEnvironment _environment;

    public UsersController(IUserService userService, IWebHostEnvironment environment)
    {
        _userService = userService;
        _environment = environment;
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
    public async Task<ActionResult<ApiResponse<string>>> UploadAvatar([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ApiResponse<string> { Code = 40001, Message = "file required" });

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var avatarUrl = $"/uploads/avatars/{fileName}";
        return Ok(new ApiResponse<string> { Data = avatarUrl });
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
