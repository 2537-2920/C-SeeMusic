using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class SeeMusicDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<MediaFile> MediaFiles { get; set; }
    public DbSet<Evaluation> Evaluations { get; set; }
    public DbSet<EvaluationSegment> EvaluationSegments { get; set; }
    public DbSet<EvaluationSuggestion> EvaluationSuggestions { get; set; }
    public DbSet<EvaluationExport> EvaluationExports { get; set; }

    public SeeMusicDbContext(DbContextOptions<SeeMusicDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MediaId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
            entity.Property(e => e.MimeType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.StoragePath).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PreparedAudioStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.PreparedAudioPath).HasMaxLength(255);
            entity.Property(e => e.PreparationErrorMessage).HasMaxLength(500);
            entity.HasIndex(e => e.MediaId).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<Evaluation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EvaluationId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ScoringProfile).IsRequired().HasMaxLength(40);
            entity.Property(e => e.PitchStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.RhythmStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Badge).IsRequired().HasMaxLength(30);
            entity.Property(e => e.SummaryText).HasMaxLength(1000);
            entity.Property(e => e.OptionsJson).HasColumnType("longtext");
            entity.Property(e => e.WarningMessagesJson).HasColumnType("longtext");
            entity.Property(e => e.PitchAnalysisJson).HasColumnType("longtext");
            entity.Property(e => e.RhythmAnalysisJson).HasColumnType("longtext");
            entity.Property(e => e.TransposeBaseJson).HasColumnType("longtext");
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.AnonymousTokenHash).HasMaxLength(120);
            entity.HasIndex(e => e.EvaluationId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne<MediaFile>().WithMany().HasForeignKey(e => e.PerformanceMediaFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MediaFile>().WithMany().HasForeignKey(e => e.ReferenceMediaFileId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EvaluationSegment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MetricType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.DeviationUnit).HasMaxLength(20);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(20);
            entity.Property(e => e.NoteText).HasMaxLength(500);
            entity.HasIndex(e => new { e.EvaluationDbId, e.MetricType, e.SortOrder });
            entity.HasOne<Evaluation>().WithMany().HasForeignKey(e => e.EvaluationDbId);
        });

        modelBuilder.Entity<EvaluationSuggestion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SuggestionType).IsRequired().HasMaxLength(30);
            entity.Property(e => e.Title).HasMaxLength(120);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(1000);
            entity.HasIndex(e => new { e.EvaluationDbId, e.SortOrder });
            entity.HasOne<Evaluation>().WithMany().HasForeignKey(e => e.EvaluationDbId);
        });

        modelBuilder.Entity<EvaluationExport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExportType).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.EvaluationDbId);
            entity.HasOne<Evaluation>().WithMany().HasForeignKey(e => e.EvaluationDbId);
            entity.HasOne<MediaFile>().WithMany().HasForeignKey(e => e.MediaFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
