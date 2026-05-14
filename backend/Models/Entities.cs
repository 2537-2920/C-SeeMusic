namespace backend.Models;

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Bio { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
}

public sealed class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class MediaFile
{
    public int Id { get; set; }
    public string MediaId { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public string Url { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public int? DurationMs { get; set; }
    public string PreparedAudioStatus { get; set; } = "pending";
    public string? PreparedAudioPath { get; set; }
    public string? PreparationErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Evaluation
{
    public int Id { get; set; }
    public string EvaluationId { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public int PerformanceMediaFileId { get; set; }
    public int? ReferenceMediaFileId { get; set; }
    public string Status { get; set; } = "queued";
    public int Progress { get; set; }
    public bool AnalyzePitch { get; set; } = true;
    public bool AnalyzeRhythm { get; set; } = true;
    public string ScoringProfile { get; set; } = "pending";
    public string PitchStatus { get; set; } = "pending";
    public string RhythmStatus { get; set; } = "pending";
    public double? TotalScore { get; set; }
    public double? PitchScore { get; set; }
    public double? RhythmScore { get; set; }
    public double? DetectedTempoBpm { get; set; }
    public double? MeanPitchDeviationCents { get; set; }
    public string Badge { get; set; } = "pending";
    public string SummaryText { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "{}";
    public string WarningMessagesJson { get; set; } = "[]";
    public string PitchAnalysisJson { get; set; } = "{}";
    public string RhythmAnalysisJson { get; set; } = "{}";
    public string TransposeBaseJson { get; set; } = "{}";
    public string ErrorMessage { get; set; } = string.Empty;
    public string? AnonymousTokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public sealed class EvaluationSegment
{
    public int Id { get; set; }
    public int EvaluationDbId { get; set; }
    public string MetricType { get; set; } = string.Empty;
    public int StartMs { get; set; }
    public int EndMs { get; set; }
    public double? Score { get; set; }
    public double? DeviationValue { get; set; }
    public string? DeviationUnit { get; set; }
    public string Severity { get; set; } = "normal";
    public string NoteText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class EvaluationSuggestion
{
    public int Id { get; set; }
    public int EvaluationDbId { get; set; }
    public string SuggestionType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class EvaluationExport
{
    public int Id { get; set; }
    public int EvaluationDbId { get; set; }
    public int MediaFileId { get; set; }
    public string ExportType { get; set; } = "pdf";
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
