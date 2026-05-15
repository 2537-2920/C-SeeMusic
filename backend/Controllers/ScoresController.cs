using System.Security.Claims;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/scores")]
public class ScoresController : ControllerBase
{
    private readonly ITranscriptionService _transcriptionService;

    public ScoresController(ITranscriptionService transcriptionService)
    {
        _transcriptionService = transcriptionService;
    }

    [HttpGet("{scoreId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ScoreDetailResponse>>> GetScore(
        string scoreId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _transcriptionService.GetScoreAsync(scoreId, TryGetCurrentUserId(), cancellationToken);
            return Ok(new ApiResponse<ScoreDetailResponse> { Data = response });
        }
        catch (Exception exception)
        {
            return exception switch
            {
                InvalidOperationException when exception.Message.Contains("未找到", StringComparison.OrdinalIgnoreCase) =>
                    NotFound(new ApiResponse<ScoreDetailResponse> { Code = 40404, Message = exception.Message }),
                InvalidOperationException => BadRequest(new ApiResponse<ScoreDetailResponse> { Code = 40001, Message = exception.Message }),
                _ => StatusCode(500, new ApiResponse<ScoreDetailResponse> { Code = 50000, Message = exception.Message })
            };
        }
    }

    private int? TryGetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        return userId;
    }
}
