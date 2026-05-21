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
    private readonly IInstantTranscriptionService _instantTranscriptionService;

    public TranscriptionController(
        ITranscriptionService transcriptionService,
        IInstantTranscriptionService instantTranscriptionService)
    {
        _transcriptionService = transcriptionService;
        _instantTranscriptionService = instantTranscriptionService;
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

    [HttpPost("instant")]
    [AllowAnonymous]
    [RequestFormLimits(MultipartBodyLengthLimit = 500_000_000)]
    [RequestSizeLimit(500_000_000)]
    public async Task<ActionResult<ApiResponse<ScoreDetailResponse>>> Instant(
        [FromForm] IFormFile audioFile,
        [FromForm] string title = "",
        [FromForm] bool separateMelody = true,
        [FromForm] bool separateAccompaniment = true,
        [FromForm] bool analyzeRhythm = true,
        CancellationToken cancellationToken = default)
    {
        if (audioFile == null || audioFile.Length == 0)
        {
            return BadRequest(new ApiResponse<ScoreDetailResponse> { Code = 40001, Message = "audioFile required" });
        }

        try
        {
            var score = await _instantTranscriptionService.TranscribeAsync(
                audioFile,
                title,
                new TranscriptionOptionsRequest
                {
                    Mode = "piano",
                    SeparateMelody = separateMelody,
                    SeparateAccompaniment = separateAccompaniment,
                    AnalyzeRhythm = analyzeRhythm
                },
                cancellationToken);

            return Ok(new ApiResponse<ScoreDetailResponse> { Data = score });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<ScoreDetailResponse>(exception);
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
