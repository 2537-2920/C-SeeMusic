using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/transcriptions")]
public class TranscriptionController : ControllerBase
{
    private readonly IMediaService _mediaService;

    public TranscriptionController(IMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    [HttpPost("analyze")]
    public ActionResult<ApiResponse<TranscriptionResult>> Analyze([FromBody] TranscriptionRequest request)
    {
        var result = _mediaService.Analyze(request);
        return Ok(new ApiResponse<TranscriptionResult> { Data = result });
    }
}
