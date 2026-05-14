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
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50).HasColumnName("username");
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.PasswordHash).IsRequired().HasColumnName("password_hash");
            entity.Property(e => e.DisplayName).HasMaxLength(100).HasColumnName("DisplayName");
            entity.Property(e => e.AvatarUrl).HasColumnName("AvatarUrl");
            entity.Property(e => e.Bio).HasMaxLength(500).HasColumnName("Bio");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // 配置 RefreshToken 实体的模型属性
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasColumnName("token");
            entity.Property(e => e.UserId).IsRequired().HasColumnName("user_id");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId);
        });

        // 配置 MediaFile 实体的模型属性
        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.ToTable("media_assets"); // 对应数据库中的 media_assets 表
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MediaId).IsRequired().HasMaxLength(50).HasColumnName("media_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255).HasColumnName("file_name");
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20).HasColumnName("type");
            entity.Property(e => e.Url).HasColumnName("url");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(e => e.MediaId).IsUnique();
        });

        // 配置 Score
        modelBuilder.Entity<Score>(entity =>
        {
            entity.ToTable("scores");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.ArtistName).HasMaxLength(100).HasColumnName("artist_name");
            entity.Property(e => e.ArrangementTag).HasMaxLength(50).HasColumnName("arrangement_tag");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.CoverUrl).HasColumnName("cover_url");
            entity.Property(e => e.FileUrl).HasColumnName("file_url");
            entity.Property(e => e.PriceCent).HasColumnName("price_cent");
            entity.Property(e => e.IsPublic).HasColumnName("is_public");
            entity.Property(e => e.DownloadCount).HasColumnName("download_count");
            entity.Property(e => e.FavoriteCount).HasColumnName("favorite_count");
            entity.Property(e => e.CommentCount).HasColumnName("comment_count");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        // 配置 ScoreCategory
        modelBuilder.Entity<ScoreCategory>(entity =>
        {
            entity.ToTable("score_categories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50).HasColumnName("name");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
        });

        // 配置 ScoreCategoryRelation
        modelBuilder.Entity<ScoreCategoryRelation>(entity =>
        {
            entity.ToTable("score_category_relations");
            entity.HasKey(e => new { e.ScoreId, e.CategoryId });
            entity.Property(e => e.ScoreId).HasColumnName("score_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            
            entity.HasOne(e => e.Score).WithMany(s => s.CategoryRelations).HasForeignKey(e => e.ScoreId);
            entity.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId);
        });

        // 配置 ScoreComment
        modelBuilder.Entity<ScoreComment>(entity =>
        {
            entity.ToTable("score_comments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(1000).HasColumnName("content");
            entity.Property(e => e.ScoreId).HasColumnName("score_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Status).HasColumnName("status");
        });

        // 配置 ScoreFavorite
        modelBuilder.Entity<ScoreFavorite>(entity =>
        {
            entity.ToTable("score_favorites");
            entity.HasKey(e => new { e.UserId, e.ScoreId });
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ScoreId).HasColumnName("score_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // 配置 ScoreDownload
        modelBuilder.Entity<ScoreDownload>(entity =>
        {
            entity.ToTable("score_downloads");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ScoreId).HasColumnName("score_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.IPAddress).HasColumnName("source_ip");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // 种子数据：初始分类
        modelBuilder.Entity<ScoreCategory>().HasData(
            new ScoreCategory { Id = 1, Name = "流行", SortOrder = 1 },
            new ScoreCategory { Id = 2, Name = "古典", SortOrder = 2 },
            new ScoreCategory { Id = 3, Name = "爵士", SortOrder = 3 },
            new ScoreCategory { Id = 4, Name = "ACG", SortOrder = 4 }
        );
    }
}
