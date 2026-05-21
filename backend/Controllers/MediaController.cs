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
    public async Task<ActionResult<ApiResponse<MediaUploadResponse>>> Upload([FromForm] MediaUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest(new ApiResponse<MediaUploadResponse> { Code = 40001, Message = "file required" });

        try
        {
            var userId = TryGetCurrentUserId();
            var result = await _mediaService.UploadAsync(request.File, request.Type, userId);
            return Ok(new ApiResponse<MediaUploadResponse> { Data = result });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse<MediaUploadResponse> { Code = 40001, Message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(500, new ApiResponse<MediaUploadResponse> { Code = 50000, Message = exception.Message });
        }
    }

    private int TryGetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return 0;
        }

        return userId;
    }
}
