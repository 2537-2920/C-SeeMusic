using System.Security.Claims;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/transcriptions")]
public class TranscriptionController : ControllerBase
{
    private readonly ITranscriptionService _transcriptionService;

    public TranscriptionController(ITranscriptionService transcriptionService)
    {
        _transcriptionService = transcriptionService;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CreateTranscriptionResponse>>> Create(
        [FromBody] CreateTranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _transcriptionService.CreateAsync(request, TryGetCurrentUserId(), cancellationToken);
            return Ok(new ApiResponse<CreateTranscriptionResponse> { Data = response });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<CreateTranscriptionResponse>(exception);
        }
    }

    [HttpGet("{jobId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TranscriptionStatusResponse>>> GetStatus(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _transcriptionService.GetStatusAsync(jobId, TryGetCurrentUserId(), cancellationToken);
            return Ok(new ApiResponse<TranscriptionStatusResponse> { Data = response });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<TranscriptionStatusResponse>(exception);
        }
    }

    [HttpPost("analyze")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TranscriptionResult>>> Analyze(
        [FromBody] TranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _transcriptionService.AnalyzeLegacyAsync(request, TryGetCurrentUserId(), cancellationToken);
            return Ok(new ApiResponse<TranscriptionResult> { Data = result });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<TranscriptionResult>(exception);
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

    private ActionResult<ApiResponse<T>> BuildErrorResponse<T>(Exception exception)
    {
        return exception switch
        {
            InvalidOperationException when exception.Message.Contains("未找到", StringComparison.OrdinalIgnoreCase) =>
                NotFound(new ApiResponse<T> { Code = 40404, Message = exception.Message }),
            InvalidOperationException => BadRequest(new ApiResponse<T> { Code = 40001, Message = exception.Message }),
            _ => StatusCode(500, new ApiResponse<T> { Code = 50000, Message = exception.Message })
        };
    }
}
