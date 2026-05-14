using System.Security.Claims;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/evaluations")]
public class EvaluationsController : ControllerBase
{
    private readonly IEvaluationService _evaluationService;

    public EvaluationsController(IEvaluationService evaluationService)
    {
        _evaluationService = evaluationService;
    }

    [HttpPost("submit")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<EvaluationSubmitResponse>>> Submit(
        [FromForm] IFormFile performanceFile,
        [FromForm] IFormFile? referenceFile,
        [FromForm] bool analyzePitch = true,
        [FromForm] bool analyzeRhythm = true,
        CancellationToken cancellationToken = default)
    {
        if (performanceFile == null || performanceFile.Length == 0)
        {
            return BadRequest(new ApiResponse<EvaluationSubmitResponse> { Code = 40001, Message = "performanceFile required" });
        }

        try
        {
            var response = await _evaluationService.SubmitAsync(
                performanceFile,
                referenceFile,
                new EvaluationOptionsRequest
                {
                    AnalyzePitch = analyzePitch,
                    AnalyzeRhythm = analyzeRhythm,
                },
                TryGetCurrentUserId(),
                cancellationToken);

            return Ok(new ApiResponse<EvaluationSubmitResponse> { Data = response });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<EvaluationSubmitResponse>(exception);
        }
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<EvaluationSubmitResponse>>> Create(
        [FromBody] CreateEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _evaluationService.CreateAsync(
                request,
                TryGetCurrentUserId(),
                cancellationToken);

            return Ok(new ApiResponse<EvaluationSubmitResponse> { Data = response });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<EvaluationSubmitResponse>(exception);
        }
    }

    [HttpGet("{evaluationId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<EvaluationStatusResponse>>> GetStatus(
        string evaluationId,
        [FromQuery] string? accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _evaluationService.GetStatusAsync(
                evaluationId,
                TryGetCurrentUserId(),
                accessToken,
                cancellationToken);

            return Ok(new ApiResponse<EvaluationStatusResponse> { Data = response });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<EvaluationStatusResponse>(exception);
        }
    }

    [HttpGet("{evaluationId}/report")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<EvaluationReportResponse>>> GetReport(
        string evaluationId,
        [FromQuery] string? accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _evaluationService.GetReportAsync(
                evaluationId,
                TryGetCurrentUserId(),
                accessToken,
                cancellationToken);

            return Ok(new ApiResponse<EvaluationReportResponse> { Data = response });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<EvaluationReportResponse>(exception);
        }
    }

    [HttpPost("{evaluationId}/exports")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<EvaluationExportResponse>>> Export(
        string evaluationId,
        [FromBody] ExportEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _evaluationService.ExportAsync(
                evaluationId,
                GetCurrentUserId(),
                request.Format,
                cancellationToken);

            return Ok(new ApiResponse<EvaluationExportResponse> { Data = response });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<EvaluationExportResponse>(exception);
        }
    }

    private int GetCurrentUserId()
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("缺少有效登录态。");
        }

        return userId.Value;
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
            UnauthorizedAccessException => StatusCode(403, new ApiResponse<T> { Code = 40301, Message = exception.Message }),
            InvalidOperationException when exception.Message.Contains("未找到", StringComparison.OrdinalIgnoreCase) =>
                NotFound(new ApiResponse<T> { Code = 40404, Message = exception.Message }),
            InvalidOperationException => BadRequest(new ApiResponse<T> { Code = 40001, Message = exception.Message }),
            _ => StatusCode(500, new ApiResponse<T> { Code = 50000, Message = exception.Message })
        };
    }
}
