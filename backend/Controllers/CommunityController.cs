using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace backend.Controllers;

[ApiController]
[Route("api/v1/community")]
public class CommunityController : ControllerBase
{
    private readonly ICommunityService _communityService;

    public CommunityController(ICommunityService communityService)
    {
        _communityService = communityService;
    }

    [Authorize]
    [HttpPost("scores")]
    public async Task<ActionResult<ApiResponse<ScoreDto>>> UploadScore(
        [FromForm] ScoreUploadRequest request,
        IFormFile scoreFile,
        IFormFile? coverFile)
    {
        if (scoreFile == null || scoreFile.Length == 0)
            return BadRequest(new ApiResponse<ScoreDto> { Code = 40001, Message = "Score file is required" });

        var userId = GetCurrentUserId();
        var result = await _communityService.UploadScoreAsync(request, scoreFile, coverFile, userId);
        
        return Ok(new ApiResponse<ScoreDto> { Data = result });
    }

    [HttpGet("scores")]
    public async Task<ActionResult<ApiResponse<List<ScoreDto>>>> GetScores(
        [FromQuery] string? keyword,
        [FromQuery] string? category,
        [FromQuery] string? sort = "latest",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var scores = await _communityService.GetScoresAsync(keyword, category, sort, page, pageSize);
        return Ok(new ApiResponse<List<ScoreDto>> { Data = scores });
    }

    [HttpGet("scores/{scoreId}")]
    public async Task<ActionResult<ApiResponse<ScoreDetailDto>>> GetScoreDetail(int scoreId)
    {
        int? userId = null;
        
        // 尝试从不同的地方获取 Token 并解析
        var authHeader = Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            try
            {
                var token = authHeader.Substring("Bearer ".Length);
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                
                // 遍历所有 Claim，寻找 ID
                var idClaim = jwtToken.Claims.FirstOrDefault(c => 
                    c.Type == "id" || 
                    c.Type == "sub" || 
                    c.Type == "nameid" || 
                    c.Type == System.Security.Claims.ClaimTypes.NameIdentifier ||
                    c.Type.EndsWith("nameidentifier") ||
                    c.Type.EndsWith("id"));

                if (idClaim != null && int.TryParse(idClaim.Value, out var id))
                {
                    userId = id;
                }
            }
            catch { }
        }

        // 如果上面没拿到，再试一次 User 对象
        if (userId == null && User.Identity?.IsAuthenticated == true)
        {
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (idClaim != null && int.TryParse(idClaim.Value, out var id)) userId = id;
        }
        
        var detail = await _communityService.GetScoreDetailAsync(scoreId, userId);
        if (detail == null) return NotFound(new ApiResponse<ScoreDetailDto> { Code = 40401, Message = "Score not found" });
        
        return Ok(new ApiResponse<ScoreDetailDto> { Data = detail });
    }

    [HttpGet("scores/{scoreId}/comments")]
    public async Task<ActionResult<ApiResponse<List<CommentDto>>>> GetComments(int scoreId)
    {
        var comments = await _communityService.GetCommentsAsync(scoreId);
        return Ok(new ApiResponse<List<CommentDto>> { Data = comments });
    }

    [Authorize]
    [HttpPost("scores/{scoreId}/comments")]
    public async Task<ActionResult<ApiResponse<string>>> AddComment(int scoreId, [FromBody] CommentRequest request)
    {
        var userId = GetCurrentUserId();
        var success = await _communityService.AddCommentAsync(scoreId, userId, request.Content);
        if (!success) return BadRequest(new ApiResponse<string> { Code = 40002, Message = "Failed to add comment" });
        return Ok(new ApiResponse<string> { Data = "success" });
    }

    [Authorize]
    [HttpPost("scores/{scoreId}/favorite")]
    public async Task<ActionResult<ApiResponse<string>>> Favorite(int scoreId)
    {
        var userId = GetCurrentUserId();
        await _communityService.ToggleFavoriteAsync(scoreId, userId, true);
        return Ok(new ApiResponse<string> { Data = "favorited" });
    }

    [Authorize]
    [HttpDelete("scores/{scoreId}/favorite")]
    public async Task<ActionResult<ApiResponse<string>>> Unfavorite(int scoreId)
    {
        var userId = GetCurrentUserId();
        await _communityService.ToggleFavoriteAsync(scoreId, userId, false);
        return Ok(new ApiResponse<string> { Data = "unfavorited" });
    }

    [Authorize]
    [HttpPost("scores/{scoreId}/download")]
    public async Task<ActionResult<ApiResponse<string>>> Download(int scoreId)
    {
        var userId = GetCurrentUserId();
        var url = await _communityService.GetDownloadUrlAsync(scoreId, userId);
        if (url == null) return NotFound(new ApiResponse<string> { Code = 40401, Message = "Score not found" });
        return Ok(new ApiResponse<string> { Data = url });
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            throw new System.InvalidOperationException("Invalid user");
        }
        return userId;
    }
}
