// 导入 JWT 认证相关命名空间
using backend.Auth;
using backend.Data;
using backend.Extensions;
using backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// 创建 Web 应用构建器
var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// JWT 配置部分
// =============================================================================

// 从配置文件中读取 JWT 密钥，如果未配置则抛出异常
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
// 验证密钥长度，必须至少 32 个字符以确保安全性
if (jwtSecret.Length < 32)
{
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");
}

// 创建 JWT 设置对象，包含令牌生成的所有必要参数
var jwtSettings = new JwtSettings
{
    Secret = jwtSecret,                                           // 签名密钥，用于生成和验证令牌
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "SeeMusic",   // 令牌发行者
    Audience = builder.Configuration["Jwt:Audience"] ?? "SeeMusic", // 令牌受众（使用者）
    ExpiresInMinutes = int.Parse(builder.Configuration["Jwt:ExpiresInMinutes"] ?? "120"), // 令牌有效期（分钟）
};

// 将 JWT 设置注册为单例服务，供 JwtTokenProvider 使用
builder.Services.AddSingleton(jwtSettings);
// 注册 JWT 令牌生成器为单例服务
builder.Services.AddSingleton<JwtTokenProvider>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseEnabled = IsDatabaseEnabled(connectionString);

if (databaseEnabled)
{
    builder.Services.AddDbContext<SeeMusicDbContext>(options =>
        options.UseMySql(connectionString!, ServerVersion.AutoDetect(connectionString!),
            mysqlOptions => mysqlOptions.EnableRetryOnFailure())
    );
}

// =============================================================================
// JWT 认证配置 - 这是令牌验证的核心机制
// =============================================================================

// 配置认证服务，设置默认的认证方案为 JWT Bearer
builder.Services.AddAuthentication(options =>
{
    // 设置默认的认证方案（用于验证用户身份）
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    // 设置默认的质询方案（用于处理未授权请求）
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
// 添加 JWT Bearer 认证处理器
.AddJwtBearer(options =>
{
    // 配置令牌验证参数，这是 JWT 认证的核心安全机制
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // 1. 验证签名密钥 - 确保令牌未被篡改
        ValidateIssuerSigningKey = true,
        // 使用配置的密钥生成签名密钥
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        
        // 2. 验证发行者 - 确保令牌来自合法的发行方
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,  // 只接受指定发行者的令牌
        
        // 3. 验证受众 - 确保令牌是发给本应用的
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,  // 只接受指定受众的令牌
        
        // 4. 验证有效期 - 确保令牌未过期（最关键的安全检查）
        ValidateLifetime = true,
        // 不允许可时钟偏差，过期即失效（生产环境建议设置为 0）
        ClockSkew = TimeSpan.Zero,
    };
});

builder.WebHost.ConfigureKestrel(opts =>
    opts.Limits.MaxRequestBodySize = 500_000_000); // 500 MB

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<EvaluationProcessingOptions>(
    builder.Configuration.GetSection("EvaluationProcessing"));
builder.Services.Configure<TranscriptionProcessingOptions>(
    builder.Configuration.GetSection("TranscriptionProcessing"));
builder.Services.AddCoreApplicationServices();

if (databaseEnabled)
{
    builder.Services.AddDatabaseBackedApplicationServices();
}

var app = builder.Build();

if (databaseEnabled)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<SeeMusicDbContext>();
        Console.WriteLine("--> [DB] Attempting to connect and migrate...");
        if (context.Database.GetMigrations().Any())
        {
            context.Database.Migrate();
            Console.WriteLine("--> [DB] Migration successful.");
        }
        else
        {
            context.Database.EnsureCreated();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--> [DB ERROR] Migration failed: {ex.Message}");
    }
}

// 无论什么模式都开启 Swagger，方便调试
app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();

var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
Directory.CreateDirectory(uploadsDir);

app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsDir),
    RequestPath = "/uploads"
});

// 禁用 HTTPS 重定向，避免本地证书引发的连接失败
// app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine("\n" + new string('!', 50));
        Console.WriteLine($"[GLOBAL ERROR] {DateTime.Now}");
        Console.WriteLine($"Path: {context.Request.Path}");
        Console.WriteLine($"Error: {ex.Message}");
        if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
        Console.WriteLine(new string('!', 50) + "\n");
        throw;
    }
});

app.MapControllers();

app.Run();

static bool IsDatabaseEnabled(string? connectionString)
{
    return !string.IsNullOrWhiteSpace(connectionString)
        && !connectionString.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}
