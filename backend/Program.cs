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
        
        // DB schema patches removed.
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