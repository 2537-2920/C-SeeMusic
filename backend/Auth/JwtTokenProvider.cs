using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace backend.Auth;

/// <summary>
/// JWT 令牌提供者 - 负责生成、验证和刷新 JWT 令牌
/// </summary>
public class JwtTokenProvider
{
    // JWT 配置设置，包含密钥和有效期等参数
    private readonly JwtSettings _settings;

    /// <summary>
    /// 构造函数，注入 JWT 配置
    /// </summary>
    public JwtTokenProvider(JwtSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// 生成访问令牌（Access Token）
    /// 用于用户身份验证，有效期较短（默认 120 分钟）
    /// </summary>
    /// <param name="userId">用户 ID，将作为 Claim 嵌入令牌</param>
    /// <param name="username">用户名，将作为 Claim 嵌入令牌</param>
    /// <returns>JWT 令牌字符串</returns>
    public string GenerateAccessToken(int userId, string username)
    {
        // 创建 Claims 集合 - 这些是嵌入令牌中的用户身份信息
        var claims = new List<Claim>
        {
            // 用户 ID Claim - 用于后续从令牌中提取用户身份
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            // 用户名 Claim - 用于显示和日志记录
            new(ClaimTypes.Name, username),
        };

        // 使用 HMAC-SHA256 算法生成签名密钥
        // 密钥来自配置文件，确保只有服务端能生成有效令牌
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        // 创建签名凭证，使用 HMAC-SHA256 算法
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        // 计算令牌过期时间（从 UTC 当前时间开始）
        var expires = DateTime.UtcNow.AddMinutes(_settings.ExpiresInMinutes);

        // 创建 JWT 安全令牌对象
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,              // 令牌发行者 - 验证时检查
            audience: _settings.Audience,          // 令牌受众 - 验证时检查
            claims: claims,                        // 用户身份声明
            expires: expires,                      // 过期时间 - 验证时检查
            signingCredentials: creds              // 签名凭证 - 用于生成签名
        );

        // 将令牌对象序列化为 JWT 字符串格式（Header.Payload.Signature）
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 生成刷新令牌（Refresh Token）
    /// 用于在 Access Token 过期后获取新的 Access Token
    /// 有效期较长（7 天），存储在数据库中
    /// </summary>
    /// <returns>64 字节的随机数（Base64 编码）</returns>
    public string GenerateRefreshToken()
    {
        // 生成 64 字节的加密安全随机数
        var randomNumber = new byte[64];
        // 使用加密安全的随机数生成器
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        // 转换为 Base64 字符串便于传输和存储
        return Convert.ToBase64String(randomNumber);
    }

    /// <summary>
    /// 验证 JWT 令牌的有效性
    /// 用于手动验证令牌（通常由中间件自动完成）
    /// </summary>
    /// <param name="token">要验证的 JWT 令牌字符串</param>
    /// <returns>如果令牌有效，返回 ClaimsPrincipal；否则返回 null</returns>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            // 生成签名密钥（与生成令牌时使用相同的密钥）
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
            // 创建 JWT 安全令牌处理器
            var handler = new JwtSecurityTokenHandler();
            // 验证令牌并提取 Claims
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                // 验证签名密钥 - 确保令牌未被篡改
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                // 验证发行者 - 确保令牌来自合法的发行方
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                // 验证受众 - 确保令牌是发给本应用的
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                // 验证有效期 - 确保令牌未过期
                ValidateLifetime = true,
                // 不允许可时钟偏差
                ClockSkew = TimeSpan.Zero,
            }, out _);

            // 令牌有效，返回包含用户 Claims 的主体对象
            return principal;
        }
        catch
        {
            // 令牌无效或已过期，返回 null
            return null;
        }
    }
}
