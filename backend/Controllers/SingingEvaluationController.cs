using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/singing/evaluate")]
public class SingingEvaluationController : ControllerBase
{
    private readonly IInstantSingingEvaluationService _instantSingingEvaluationService;
    private readonly ITransposeSuggestionService _transposeSuggestionService;
    private readonly IPdfExportService _pdfExportService;

    public SingingEvaluationController(
        IInstantSingingEvaluationService instantSingingEvaluationService,
        ITransposeSuggestionService transposeSuggestionService,
        IPdfExportService pdfExportService)
    {
        _instantSingingEvaluationService = instantSingingEvaluationService;
        _transposeSuggestionService = transposeSuggestionService;
        _pdfExportService = pdfExportService;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<EvaluationReportResponse>>> Submit(
        [FromForm] IFormFile performanceFile,
        [FromForm] IFormFile? referenceFile,
        [FromForm] string userAudioType = "with_accompaniment",
        [FromForm] string feedbackLanguage = "zh-CN",
        [FromForm] string scoringModel = "balanced",
        [FromForm] int rhythmThresholdMs = 50,
        [FromForm] bool analyzePitch = true,
        [FromForm] bool analyzeRhythm = true,
        CancellationToken cancellationToken = default)
    {
        if (performanceFile == null || performanceFile.Length == 0)
        {
            return BadRequest(new ApiResponse<EvaluationReportResponse> { Code = 40001, Message = "performanceFile required" });
        }

        if (referenceFile == null || referenceFile.Length == 0)
        {
            return BadRequest(new ApiResponse<EvaluationReportResponse> { Code = 40001, Message = "referenceFile required" });
        }

        try
        {
            var response = await _instantSingingEvaluationService.EvaluateAsync(
                performanceFile,
                referenceFile,
                new EvaluationOptionsRequest
                {
                    AnalyzePitch = analyzePitch,
                    AnalyzeRhythm = analyzeRhythm,
                    UserAudioType = userAudioType,
                    FeedbackLanguage = feedbackLanguage,
                    ScoringModel = scoringModel,
                    RhythmThresholdMs = rhythmThresholdMs,
                },
                cancellationToken);

            return Ok(new ApiResponse<EvaluationReportResponse> { Data = response });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<EvaluationReportResponse>(exception);
        }
    }

    [HttpPost("transpose-suggestion")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<TransposeSuggestionResponse>> GetTransposeSuggestion(
        [FromBody] TransposeSuggestionRequest request)
    {
        if (request?.TransposeBase == null)
        {
            return BadRequest(new ApiResponse<TransposeSuggestionResponse> { Code = 40001, Message = "transposeBase required" });
        }

        try
        {
            var response = _transposeSuggestionService.Build(request);
            return Ok(new ApiResponse<TransposeSuggestionResponse> { Data = response });
        }
        catch (Exception exception)
        {
            return BuildErrorResponse<TransposeSuggestionResponse>(exception);
        }
    }

    [HttpPost("export-pdf")]
    [AllowAnonymous]
    public IActionResult ExportPdf([FromBody] EvaluationPdfExportRequest request)
    {
        if (request?.Report?.Summary == null)
        {
            return BadRequest(new ApiResponse<object> { Code = 40001, Message = "report required" });
        }

        try
        {
            var bytes = _pdfExportService.Export(request.Report);
            var fileName = string.IsNullOrWhiteSpace(request.Report.Summary.AnalysisId)
                ? "singing-evaluation-report.pdf"
                : $"singing-evaluation-{request.Report.Summary.AnalysisId}.pdf";
            return File(bytes, "application/pdf", fileName);
        }
        catch (Exception exception)
        {
            return BuildFileActionErrorResponse(exception);
        }
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

    private IActionResult BuildFileActionErrorResponse(Exception exception)
    {
        return exception switch
        {
            InvalidOperationException when exception.Message.Contains("未找到", StringComparison.OrdinalIgnoreCase) =>
                NotFound(new ApiResponse<object> { Code = 40404, Message = exception.Message }),
            InvalidOperationException => BadRequest(new ApiResponse<object> { Code = 40001, Message = exception.Message }),
            _ => StatusCode(500, new ApiResponse<object> { Code = 50000, Message = exception.Message })
        };
    }
}
