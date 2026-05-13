using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

/// <summary>
/// 数据库上下文类，用于管理用户、刷新令牌和媒体文件的数据访问
/// </summary>
public class SeeMusicDbContext : DbContext
{
    /// <summary>
    /// 用户实体集合
    /// </summary>
    public DbSet<User> Users { get; set; }
    /// <summary>
    /// 刷新令牌实体集合
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    /// <summary>
    /// 媒体文件实体集合
    /// </summary>
    public DbSet<MediaFile> MediaFiles { get; set; }

    /// <summary>
    /// 初始化 SeeMusicDbContext 实例
    /// </summary>
    /// <param name="options">数据库上下文配置选项</param>
    public SeeMusicDbContext(DbContextOptions<SeeMusicDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// 配置实体模型的结构，定义表关系和约束
    /// </summary>
    /// <param name="modelBuilder">模型构建器，用于配置实体类型</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置 User 实体的模型属性
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.Bio).HasMaxLength(500);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // 配置 RefreshToken 实体的模型属性
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId);
        });

        // 配置 MediaFile 实体的模型属性
        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MediaId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.MediaId).IsUnique();
        });
    }
}
