using backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public ActionResult<ApiResponse<HealthStatusResponse>> Get()
    {
        return Ok(new ApiResponse<HealthStatusResponse>
        {
            Data = new HealthStatusResponse
            {
                Status = "ok",
                ServiceAvailable = true,
                TimestampUtc = DateTime.UtcNow
            }
        });
    }
}
