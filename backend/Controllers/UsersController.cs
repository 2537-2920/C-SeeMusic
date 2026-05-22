using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers;

/// <summary>
/// 用户管理控制器 - 处理用户相关的 HTTP 请求
/// 所有接口都需要 JWT 令牌认证（[Authorize] 特性）
/// </summary>
[ApiController]
[Route("api/v1/users")]  // API 路由前缀
[Authorize]  // 启用 JWT 认证 - 所有接口都需要有效的访问令牌
public class UsersController : ControllerBase
{
    // 用户服务接口，处理用户相关的业务逻辑
    private readonly IUserService _userService;
    // 评估服务接口，处理用户评估历史查询
    private readonly IEvaluationService _evaluationService;

    /// <summary>
    /// 构造函数，依赖注入服务实例
    /// </summary>
    public UsersController(IUserService userService, IEvaluationService evaluationService)
    {
        _userService = userService;
        _evaluationService = evaluationService;
    }

    /// <summary>
    /// 获取当前登录用户的信息
    /// GET /api/v1/users/me
    /// </summary>
    [HttpGet("me")]
    public ActionResult<ApiResponse<UserDto>> GetCurrentUser()
    {
        // 从 JWT 令牌中提取用户 ID
        var userId = GetCurrentUserId();
        // 查询用户详细信息
        var user = _userService.GetCurrentUser(userId);
        // 返回用户数据
        return Ok(new ApiResponse<UserDto> { Data = user });
    }

    /// <summary>
    /// 更新当前用户的个人资料
    /// PUT /api/v1/users/me
    /// </summary>
    [HttpPut("me")]
    public ActionResult<ApiResponse<UserDto>> UpdateProfile([FromBody] UserDto profile)
    {
        // 从 JWT 令牌中提取用户 ID
        var userId = GetCurrentUserId();
        // 更新用户资料
        var updated = _userService.UpdateProfile(userId, profile);
        return Ok(new ApiResponse<UserDto> { Data = updated });
    }

    /// <summary>
    /// 上传用户头像（multipart/form-data）
    /// POST /api/v1/users/me/avatar
    /// </summary>
    [HttpPost("me/avatar")]
    public async Task<ActionResult<ApiResponse<string>>> UploadAvatar([FromForm] AvatarUploadRequest request)
    {
        // 验证上传文件是否存在
        if (request.File == null || request.File.Length == 0)
            return BadRequest(new ApiResponse<string> { Code = 40001, Message = "file required" });

        // 从 JWT 令牌中提取用户 ID
        var userId = GetCurrentUserId();
        // 上传头像并获取访问 URL
        var avatarUrl = await _userService.UploadAvatarAsync(userId, request.File);
        return Ok(new ApiResponse<string> { Data = avatarUrl });
    }

    /// <summary>
    /// 获取用户仪表盘数据（统计数据 + 周使用量）
    /// GET /api/v1/users/me/dashboard
    /// </summary>
    [HttpGet("me/dashboard")]
    public async Task<ActionResult<ApiResponse<DashboardResponse>>> GetDashboard()
    {
        // 从 JWT 令牌中提取用户 ID
        var userId = GetCurrentUserId();
        // 获取仪表盘数据（包含统计和周使用量图表）
        var dashboard = await _userService.GetDashboardAsync(userId);
        return Ok(new ApiResponse<DashboardResponse> { Data = dashboard });
    }

    /// <summary>
    /// 获取用户偏好设置（主题、导出格式等）
    /// GET /api/v1/users/me/preferences
    /// </summary>
    [HttpGet("me/preferences")]
    public async Task<ActionResult<ApiResponse<UserPreferencesDto>>> GetPreferences()
    {
        // 从 JWT 令牌中提取用户 ID
        var userId = GetCurrentUserId();
        // 查询用户偏好设置
        var preferences = await _userService.GetPreferencesAsync(userId);
        return Ok(new ApiResponse<UserPreferencesDto> { Data = preferences });
    }

    /// <summary>
    /// 更新用户偏好设置
    /// PUT /api/v1/users/me/preferences
    /// </summary>
    [HttpPut("me/preferences")]
    public async Task<ActionResult<ApiResponse<UserPreferencesDto>>> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
    {
        // 从 JWT 令牌中提取用户 ID
        var userId = GetCurrentUserId();
        // 保存偏好设置
        var preferences = await _userService.UpdatePreferencesAsync(userId, request);
        return Ok(new ApiResponse<UserPreferencesDto> { Data = preferences });
    }

    /// <summary>
    /// 修改登录密码
    /// PUT /api/v1/users/me/password
    /// </summary>
    [HttpPut("me/password")]
    public ActionResult<ApiResponse<string>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            // 从 JWT 令牌中提取用户 ID
            var userId = GetCurrentUserId();
            // 验证当前密码并设置新密码
            _userService.ChangePassword(userId, request.CurrentPassword, request.NewPassword);
            return Ok(new ApiResponse<string> { Data = "密码修改成功" });
        }
        catch (InvalidOperationException ex)
        {
            // 处理业务异常（如密码错误）
            return BadRequest(new ApiResponse<string> { Code = 40001, Message = ex.Message });
        }
    }

    /// <summary>
    /// 获取用户的评估历史记录（分页）
    /// GET /api/v1/users/me/evaluations
    /// </summary>
    [HttpGet("me/evaluations")]
    public async Task<ActionResult<ApiResponse<EvaluationHistoryResponse>>> GetEvaluationHistory(
        [FromQuery] int page = 1,           // 页码，默认第 1 页
        [FromQuery] int pageSize = 20,      // 每页数量，默认 20 条
        CancellationToken cancellationToken = default)
    {
        // 从 JWT 令牌中提取用户 ID
        var userId = GetCurrentUserId();
        // 查询评估历史（分页）
        var history = await _evaluationService.GetHistoryAsync(userId, page, pageSize, cancellationToken);
        return Ok(new ApiResponse<EvaluationHistoryResponse> { Data = history });
    }

    /// <summary>
    /// 从 JWT 令牌中提取当前登录用户的 ID
    /// 这是认证流程的关键步骤：Token → Claims → UserId
    /// </summary>
    /// <returns>用户 ID</returns>
    private int GetCurrentUserId()
    {
        // 从 Claims 集合中查找用户 ID（NameIdentifier 类型）
        // 这个 Claim 是在生成 JWT 令牌时嵌入的
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        
        // 验证 Claim 是否存在且格式正确
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            // 如果令牌中没有用户 ID 或格式错误，抛出异常
            throw new InvalidOperationException("Invalid user");
        }
        
        return userId;
    }
}
