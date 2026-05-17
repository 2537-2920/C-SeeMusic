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
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<MediaFile> MediaFiles { get; set; }
    
    // 社区相关
    public DbSet<Score> Scores { get; set; }
    public DbSet<ScoreCategory> ScoreCategories { get; set; }
    public DbSet<ScoreCategoryRelation> ScoreCategoryRelations { get; set; }
    public DbSet<ScoreComment> ScoreComments { get; set; }
    public DbSet<ScoreFavorite> ScoreFavorites { get; set; }
    public DbSet<ScoreDownload> ScoreDownloads { get; set; }

    public SeeMusicDbContext(DbContextOptions<SeeMusicDbContext> options) : base(options)
    {
    }

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

        // 配置 Score
        modelBuilder.Entity<Score>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ArtistName).HasMaxLength(100);
            entity.Property(e => e.ArrangementTag).HasMaxLength(50);

            // 导航属性
            entity.HasOne(e => e.Owner)
                  .WithMany()
                  .HasForeignKey(e => e.OwnerUserId);

            entity.HasOne(e => e.CoverMediaFile)
                  .WithMany()
                  .HasForeignKey(e => e.CoverMediaFileId)
                  .IsRequired(false);

            entity.HasOne(e => e.SourceMediaFile)
                  .WithMany()
                  .HasForeignKey(e => e.SourceMediaFileId);
        });

        // 配置 ScoreCategory
        modelBuilder.Entity<ScoreCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(50);
        });

        // 配置 ScoreCategoryRelation
        modelBuilder.Entity<ScoreCategoryRelation>(entity =>
        {
            entity.HasKey(e => new { e.ScoreDbId, e.CategoryId });
            
            entity.HasOne(e => e.Score).WithMany(s => s.CategoryRelations).HasForeignKey(e => e.ScoreDbId);
            entity.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId);
        });

        // 配置 ScoreComment
        modelBuilder.Entity<ScoreComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(1000);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId);

            entity.HasOne<Score>()
                  .WithMany(s => s.Comments)
                  .HasForeignKey(e => e.ScoreDbId);
        });

        // 配置 ScoreFavorite
        modelBuilder.Entity<ScoreFavorite>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ScoreDbId });
        });

        // 配置 ScoreDownload
        modelBuilder.Entity<ScoreDownload>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // 种子数据：初始分类
        modelBuilder.Entity<ScoreCategory>().HasData(
            new ScoreCategory { Id = 1, Name = "流行", Slug = "pop", SortOrder = 1 },
            new ScoreCategory { Id = 2, Name = "古典", Slug = "classical", SortOrder = 2 },
            new ScoreCategory { Id = 3, Name = "爵士", Slug = "jazz", SortOrder = 3 },
            new ScoreCategory { Id = 4, Name = "ACG", Slug = "acg", SortOrder = 4 }
        );
    }
}
