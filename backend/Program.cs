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
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SeeMusicDbContext>();
    context.Database.EnsureCreated();

    // --- 数据库结构暴力同步与修复 ---
    try {
        // 1. 强制在 Scores 表中添加缺失的核心业务字段
        var queries = new[] {
            "ALTER TABLE Scores ADD COLUMN IF NOT EXISTS CoverUrl LongText;",
            "ALTER TABLE Scores ADD COLUMN IF NOT EXISTS PrimaryCategory VARCHAR(100);",
            "ALTER TABLE Scores ADD COLUMN IF NOT EXISTS DownloadCount INT DEFAULT 0;",
            "ALTER TABLE Scores ADD COLUMN IF NOT EXISTS FavoriteCount INT DEFAULT 0;",
            "ALTER TABLE Scores ADD COLUMN IF NOT EXISTS CommentCount INT DEFAULT 0;",
            "ALTER TABLE Scores ADD COLUMN IF NOT EXISTS ShareCount INT DEFAULT 0;"
        };

        foreach (var sql in queries) {
            try { context.Database.ExecuteSqlRaw(sql); } catch { /* 容错处理 */ }
        }
        
        // 2. 确保至少有一个 ID 为 1 的用户存在 (否则乐谱列表里发布者会是空白)
        if (!context.Users.Any(u => u.Id == 1)) {
            context.Database.ExecuteSqlRaw("INSERT INTO Users (Id, Username, DisplayName, Email, PasswordHash, Bio, CreatedAt, LastLoginAt) VALUES (1, 'creator', '灵感创作者', 'test@test.com', 'pwd', 'I love music', NOW(), NOW());");
            Console.WriteLine("--> [DB FIX] Created default creator user.");
        }

        Console.WriteLine("--> [DB FIX] Database synchronization complete.");
    } catch (Exception ex) {
        Console.WriteLine("--> [DB ERROR] Repair failed: " + ex.Message);
    }
    // ----------------------------

    // 自动填充测试数据
    DbSeeder.Seed(context);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); 
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
