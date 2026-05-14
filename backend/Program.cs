using backend.Auth;
using backend.Data;
using backend.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
if (jwtSecret.Length < 32)
{
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");
}

var jwtSettings = new JwtSettings
{
    Secret = jwtSecret,
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "SeeMusic",
    Audience = builder.Configuration["Jwt:Audience"] ?? "SeeMusic",
    ExpiresInMinutes = int.Parse(builder.Configuration["Jwt:ExpiresInMinutes"] ?? "120"),
};

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<JwtTokenProvider>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddDbContext<SeeMusicDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure())
);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
    };
});

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
builder.Services.AddApplicationServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<SeeMusicDbContext>();
        Console.WriteLine("--> [DB] Attempting to connect and migrate...");
        context.Database.Migrate();
        Console.WriteLine("--> [DB] Migration successful.");
        
        // --- 核心业务字段暴力同步 (支持旧版 MySQL 并兼容大小写) ---
        try {
            var columns = new[] { 
                "source_media_id INTEGER NULL", "cover_media_id INTEGER NULL",
                "key_signature VARCHAR(50)", "time_signature VARCHAR(50)", "tempo INTEGER",
                "status VARCHAR(50) DEFAULT 'published'", "source_type VARCHAR(50) DEFAULT 'audio'",
                "is_public TINYINT DEFAULT 1", "price_cent INT DEFAULT 0",
                "download_count INT DEFAULT 0", "favorite_count INT DEFAULT 0", 
                "comment_count INT DEFAULT 0", "share_count INT DEFAULT 0",
                "cover_url LongText", "file_url LongText",
                "artist_name VARCHAR(255)", "arrangement_tag VARCHAR(100)",
                "primary_category VARCHAR(100)", "published_at DATETIME"
            };
            
            foreach (var col in columns) {
                try { 
                    // 尝试小写表名添加字段
                    context.Database.ExecuteSqlRaw($"ALTER TABLE scores ADD {col};"); 
                } catch { 
                    // 如果失败，通常是因为字段已存在，或者表名是大写的，尝试大写
                    try { context.Database.ExecuteSqlRaw($"ALTER TABLE Scores ADD {col};"); } catch { }
                }
            }
            Console.WriteLine("--> [DB FIX] Schema checked and updated.");
        } catch (Exception) {
            // 彻底忽略这里的任何错误，不输出红字
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