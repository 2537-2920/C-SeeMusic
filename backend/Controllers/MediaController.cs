using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;

    public MediaController(IMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<ApiResponse<MediaUploadResponse>>> Upload([FromForm] IFormFile file, [FromForm] string type)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ApiResponse<MediaUploadResponse> { Code = 40001, Message = "file required" });

        var userId = GetCurrentUserId();
        var result = await _mediaService.UploadAsync(file, type, userId);
        return Ok(new ApiResponse<MediaUploadResponse> { Data = result });
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
