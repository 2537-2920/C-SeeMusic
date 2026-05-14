using System.Text;
using System.Text.Json;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class EvaluationService : IEvaluationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SeeMusicDbContext _dbContext;
    private readonly IMediaService _mediaService;
    private readonly IAudioPreparationService _audioPreparationService;
    private readonly IPitchAnalysisService _pitchAnalysisService;
    private readonly IRhythmEvaluationService _rhythmEvaluationService;
    private readonly IEvaluationScoringService _evaluationScoringService;
    private readonly IEvaluationTaskQueue _taskQueue;
    private readonly IAnonymousEvaluationAccessTokenService _accessTokenService;
    private readonly IWebHostEnvironment _environment;
    private readonly EvaluationExecutionPlanner _executionPlanner;
    private readonly ILogger<EvaluationService> _logger;

    public EvaluationService(
        SeeMusicDbContext dbContext,
        IMediaService mediaService,
        IAudioPreparationService audioPreparationService,
        IPitchAnalysisService pitchAnalysisService,
        IRhythmEvaluationService rhythmEvaluationService,
        IEvaluationScoringService evaluationScoringService,
        IEvaluationTaskQueue taskQueue,
        IAnonymousEvaluationAccessTokenService accessTokenService,
        IWebHostEnvironment environment,
        IOptions<EvaluationProcessingOptions> processingOptions,
        ILogger<EvaluationService> logger)
    {
        _dbContext = dbContext;
        _mediaService = mediaService;
        _audioPreparationService = audioPreparationService;
        _pitchAnalysisService = pitchAnalysisService;
        _rhythmEvaluationService = rhythmEvaluationService;
        _evaluationScoringService = evaluationScoringService;
        _taskQueue = taskQueue;
        _accessTokenService = accessTokenService;
        _environment = environment;
        _executionPlanner = new EvaluationExecutionPlanner(processingOptions.Value);
        _logger = logger;
    }

    public async Task<EvaluationSubmitResponse> SubmitAsync(
        IFormFile performanceFile,
        IFormFile? referenceFile,
        EvaluationOptionsRequest options,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOptions = (options ?? new EvaluationOptionsRequest()).Normalize();
        var performanceUpload = await _mediaService.UploadAsync(
            performanceFile,
            ResolveMediaType(performanceFile.FileName),
            userId);

        MediaUploadResponse? referenceUpload = null;
        if (referenceFile != null)
        {
            referenceUpload = await _mediaService.UploadAsync(
                referenceFile,
                ResolveMediaType(referenceFile.FileName),
                userId);
        }

        return await CreateAsync(new CreateEvaluationRequest
        {
            PerformanceMediaId = performanceUpload.MediaId,
            ReferenceMediaId = referenceUpload?.MediaId,
            Options = normalizedOptions
        }, userId, cancellationToken);
    }

    public async Task<EvaluationSubmitResponse> CreateAsync(
        CreateEvaluationRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOptions = (request.Options ?? new EvaluationOptionsRequest()).Normalize();
        var performanceMedia = await _dbContext.MediaFiles
            .SingleOrDefaultAsync(item => item.MediaId == request.PerformanceMediaId, cancellationToken)
            ?? throw new InvalidOperationException("未找到演唱素材。");

        MediaFile? referenceMedia = null;
        if (!string.IsNullOrWhiteSpace(request.ReferenceMediaId))
        {
            referenceMedia = await _dbContext.MediaFiles
                .SingleOrDefaultAsync(item => item.MediaId == request.ReferenceMediaId, cancellationToken)
                ?? throw new InvalidOperationException("未找到参考素材。");
        }

        var anonymousAccessToken = userId.HasValue ? null : _accessTokenService.GenerateToken();
        var evaluation = new Evaluation
        {
            EvaluationId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            PerformanceMediaFileId = performanceMedia.Id,
            ReferenceMediaFileId = referenceMedia?.Id,
            Status = "queued",
            Progress = 0,
            AnalyzePitch = normalizedOptions.AnalyzePitch,
            AnalyzeRhythm = normalizedOptions.AnalyzeRhythm,
            ScoringProfile = "pending",
            PitchStatus = normalizedOptions.AnalyzePitch ? "pending" : "skipped",
            RhythmStatus = normalizedOptions.AnalyzeRhythm ? "pending" : "skipped",
            Badge = LocalizedText.WaitingBadge(normalizedOptions),
            SummaryText = LocalizedText.PendingSummary(normalizedOptions),
            OptionsJson = JsonSerializer.Serialize(normalizedOptions, SerializerOptions),
            WarningMessagesJson = "[]",
            PitchAnalysisJson = JsonSerializer.Serialize(new PitchAnalysisDto(), SerializerOptions),
            RhythmAnalysisJson = JsonSerializer.Serialize(new RhythmAnalysisDto(), SerializerOptions),
            TransposeBaseJson = JsonSerializer.Serialize(new TransposeBaseDto
            {
                Summary = LocalizedText.TransposePending(normalizedOptions)
            }, SerializerOptions),
            AnonymousTokenHash = anonymousAccessToken == null ? null : _accessTokenService.HashToken(anonymousAccessToken),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _dbContext.Evaluations.Add(evaluation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_executionPlanner.ShouldProcessSynchronously(performanceMedia, referenceMedia, normalizedOptions))
        {
            await ProcessQueuedEvaluationAsync(evaluation.Id, cancellationToken);
        }
        else
        {
            await _taskQueue.QueueAsync(evaluation.Id, cancellationToken);
        }

        var refreshedEvaluation = await _dbContext.Evaluations
            .SingleAsync(item => item.Id == evaluation.Id, cancellationToken);

        return new EvaluationSubmitResponse
        {
            EvaluationId = refreshedEvaluation.EvaluationId,
            Status = refreshedEvaluation.Status,
            Progress = refreshedEvaluation.Progress,
            ReportPreview = string.Equals(refreshedEvaluation.Status, "succeeded", StringComparison.OrdinalIgnoreCase)
                ? await BuildReportAsync(refreshedEvaluation.Id, cancellationToken)
                : null,
            AnonymousAccessToken = anonymousAccessToken,
            Warnings = DeserializeWarnings(refreshedEvaluation.WarningMessagesJson),
        };
    }

    public async Task<EvaluationStatusResponse> GetStatusAsync(
        string evaluationId,
        int? userId,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await GetEvaluationByPublicIdAsync(evaluationId, cancellationToken);
        await EnsureAccessAsync(evaluation, userId, accessToken);
        return MapStatusResponse(evaluation, DeserializeOptions(evaluation.OptionsJson));
    }

    public async Task<EvaluationReportResponse> GetReportAsync(
        string evaluationId,
        int? userId,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await GetEvaluationByPublicIdAsync(evaluationId, cancellationToken);
        await EnsureAccessAsync(evaluation, userId, accessToken);
        return await BuildReportAsync(evaluation.Id, cancellationToken);
    }

    public async Task<TransposeSuggestionResponse> GetTransposeSuggestionAsync(
        string evaluationId,
        int? userId,
        string? accessToken,
        TransposeSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await GetEvaluationByPublicIdAsync(evaluationId, cancellationToken);
        await EnsureAccessAsync(evaluation, userId, accessToken);

        var options = DeserializeOptions(evaluation.OptionsJson);
        var report = await BuildReportAsync(evaluation.Id, cancellationToken);
        return BuildTransposeSuggestion(report.TransposeBase, request, options);
    }

    public async Task<EvaluationHistoryResponse> GetHistoryAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query =
            from evaluation in _dbContext.Evaluations
            join performanceMedia in _dbContext.MediaFiles on evaluation.PerformanceMediaFileId equals performanceMedia.Id
            where evaluation.UserId == userId
            orderby evaluation.CreatedAt descending
            select new EvaluationHistoryItemDto
            {
                EvaluationId = evaluation.EvaluationId,
                Status = evaluation.Status,
                TotalScore = evaluation.TotalScore,
                PitchScore = evaluation.PitchScore,
                RhythmScore = evaluation.RhythmScore,
                ScoringProfile = evaluation.ScoringProfile,
                PerformanceFileName = performanceMedia.FileName,
                CreatedAt = evaluation.CreatedAt,
            };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new EvaluationHistoryResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items,
        };
    }

    public async Task<EvaluationExportResponse> ExportAsync(
        string evaluationId,
        int userId,
        string format,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("当前仅支持导出 PDF 报告。");
        }

        var evaluation = await GetEvaluationByPublicIdAsync(evaluationId, cancellationToken);
        if (evaluation.UserId != userId)
        {
            throw new UnauthorizedAccessException("只能导出自己的评估报告。");
        }

        var report = await BuildReportAsync(evaluation.Id, cancellationToken);
        var exportDirectory = Path.Combine(_environment.ContentRootPath, "uploads", "exports");
        Directory.CreateDirectory(exportDirectory);

        var fileName = $"evaluation_{evaluation.EvaluationId}.pdf";
        var relativePath = Path.Combine("exports", fileName).Replace('\\', '/');
        var absolutePath = Path.Combine(exportDirectory, fileName);
        var bytes = MinimalPdfWriter.Write(report, evaluation.EvaluationId);
        await File.WriteAllBytesAsync(absolutePath, bytes, cancellationToken);

        var mediaFile = new MediaFile
        {
            MediaId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            FileName = fileName,
            Type = "document",
            MimeType = "application/pdf",
            FileSize = bytes.Length,
            Url = $"/uploads/{relativePath}",
            StoragePath = relativePath,
            PreparedAudioStatus = "not_applicable",
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.MediaFiles.Add(mediaFile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.EvaluationExports.Add(new EvaluationExport
        {
            EvaluationDbId = evaluation.Id,
            MediaFileId = mediaFile.Id,
            ExportType = "pdf",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EvaluationExportResponse
        {
            EvaluationId = evaluation.EvaluationId,
            ExportType = "pdf",
            FileName = fileName,
            DownloadUrl = mediaFile.Url,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public async Task ProcessQueuedEvaluationAsync(int evaluationDbId, CancellationToken cancellationToken = default)
    {
        var evaluation = await _dbContext.Evaluations
            .SingleOrDefaultAsync(item => item.Id == evaluationDbId, cancellationToken);

        if (evaluation == null)
        {
            return;
        }

        if (string.Equals(evaluation.Status, "succeeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(evaluation.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var options = DeserializeOptions(evaluation.OptionsJson);

        try
        {
            evaluation.Status = "processing";
            evaluation.Progress = 10;
            evaluation.StartedAt ??= DateTime.UtcNow;
            evaluation.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var performanceMedia = await _dbContext.MediaFiles
                .SingleOrDefaultAsync(item => item.Id == evaluation.PerformanceMediaFileId, cancellationToken);
            if (performanceMedia == null)
            {
                await MarkFailedAsync(evaluation, LocalizedText.MissingPerformance(options), cancellationToken);
                return;
            }

            var performanceAudio = await _audioPreparationService.PrepareAsync(performanceMedia, cancellationToken);
            if (!string.Equals(performanceAudio.Status, "ready", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(performanceAudio.AbsolutePath))
            {
                await MarkFailedAsync(
                    evaluation,
                    performanceAudio.ErrorMessage ?? LocalizedText.PerformancePreparationFailed(options),
                    cancellationToken);
                return;
            }

            evaluation.Progress = 35;
            evaluation.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            PreparedAudioResult? referenceAudio = null;
            MediaFile? referenceMedia = null;
            var pipelineWarnings = new List<string>();
            if (evaluation.ReferenceMediaFileId.HasValue)
            {
                referenceMedia = await _dbContext.MediaFiles
                    .SingleOrDefaultAsync(item => item.Id == evaluation.ReferenceMediaFileId.Value, cancellationToken);

                if (referenceMedia != null)
                {
                    referenceAudio = await _audioPreparationService.PrepareAsync(referenceMedia, cancellationToken);
                    if (!string.Equals(referenceAudio.Status, "ready", StringComparison.OrdinalIgnoreCase))
                    {
                        pipelineWarnings.Add(referenceAudio.ErrorMessage ?? LocalizedText.ReferencePreparationFailed(options));
                        referenceAudio = null;
                    }
                }
                else
                {
                    pipelineWarnings.Add(LocalizedText.MissingReference(options));
                }
            }

            PitchAnalysisResult pitchResult;
            if (!evaluation.AnalyzePitch)
            {
                pitchResult = new PitchAnalysisResult
                {
                    Status = "skipped",
                    Summary = LocalizedText.PitchDisabled(options),
                };
            }
            else
            {
                pitchResult = _pitchAnalysisService.Analyze(
                    performanceAudio.AbsolutePath,
                    referenceAudio?.AbsolutePath,
                    options);
            }

            evaluation.Progress = 65;
            evaluation.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            RhythmEvaluationResult rhythmResult;
            if (!evaluation.AnalyzeRhythm)
            {
                rhythmResult = new RhythmEvaluationResult
                {
                    Status = "skipped",
                    ThresholdMs = options.RhythmThresholdMs,
                    Summary = LocalizedText.RhythmDisabled(options),
                };
            }
            else
            {
                rhythmResult = _rhythmEvaluationService.Analyze(
                    performanceAudio.AbsolutePath,
                    referenceAudio?.AbsolutePath,
                    options);
            }

            var aggregate = _evaluationScoringService.Score(pitchResult, rhythmResult, options);
            aggregate.Warnings.AddRange(pipelineWarnings);
            aggregate.Warnings = aggregate.Warnings.Distinct().ToList();
            var transposeBase = BuildTransposeBase(pitchResult, referenceMedia, options);

            await PersistArtifactsAsync(evaluation.Id, pitchResult, rhythmResult, aggregate, cancellationToken);

            evaluation.Status = aggregate.Status;
            evaluation.Progress = 100;
            evaluation.ScoringProfile = aggregate.ScoringProfile;
            evaluation.PitchStatus = aggregate.PitchStatus;
            evaluation.RhythmStatus = aggregate.RhythmStatus;
            evaluation.TotalScore = aggregate.TotalScore;
            evaluation.PitchScore = pitchResult.Score;
            evaluation.RhythmScore = rhythmResult.Score;
            evaluation.DetectedTempoBpm = rhythmResult.PerformanceTempoBpm;
            evaluation.MeanPitchDeviationCents = pitchResult.MeanDeviationCents;
            evaluation.Badge = aggregate.Badge;
            evaluation.SummaryText = aggregate.SummaryText;
            evaluation.WarningMessagesJson = JsonSerializer.Serialize(aggregate.Warnings, SerializerOptions);
            evaluation.PitchAnalysisJson = JsonSerializer.Serialize(BuildPitchAnalysisDto(pitchResult), SerializerOptions);
            evaluation.RhythmAnalysisJson = JsonSerializer.Serialize(BuildRhythmAnalysisDto(rhythmResult), SerializerOptions);
            evaluation.TransposeBaseJson = JsonSerializer.Serialize(transposeBase, SerializerOptions);
            evaluation.ErrorMessage = aggregate.ErrorMessage;
            evaluation.FinishedAt = DateTime.UtcNow;
            evaluation.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to process evaluation {EvaluationDbId}", evaluationDbId);
            var tracked = await _dbContext.Evaluations.SingleOrDefaultAsync(item => item.Id == evaluationDbId, cancellationToken);
            if (tracked != null)
            {
                await MarkFailedAsync(tracked, $"{LocalizedText.ExceptionPrefix(options)}{exception.Message}", cancellationToken);
            }
        }
    }

    private async Task PersistArtifactsAsync(
        int evaluationDbId,
        PitchAnalysisResult pitchResult,
        RhythmEvaluationResult rhythmResult,
        EvaluationAggregateResult aggregate,
        CancellationToken cancellationToken)
    {
        var existingSegments = await _dbContext.EvaluationSegments
            .Where(item => item.EvaluationDbId == evaluationDbId)
            .ToListAsync(cancellationToken);
        if (existingSegments.Count > 0)
        {
            _dbContext.EvaluationSegments.RemoveRange(existingSegments);
        }

        var existingSuggestions = await _dbContext.EvaluationSuggestions
            .Where(item => item.EvaluationDbId == evaluationDbId)
            .ToListAsync(cancellationToken);
        if (existingSuggestions.Count > 0)
        {
            _dbContext.EvaluationSuggestions.RemoveRange(existingSuggestions);
        }

        var segmentSortOrder = 0;
        foreach (var segment in pitchResult.Segments.Concat(rhythmResult.Segments))
        {
            _dbContext.EvaluationSegments.Add(new EvaluationSegment
            {
                EvaluationDbId = evaluationDbId,
                MetricType = segment.MetricType,
                StartMs = segment.StartMs,
                EndMs = segment.EndMs,
                Score = segment.Score,
                DeviationValue = segment.DeviationValue,
                DeviationUnit = segment.DeviationUnit,
                Severity = segment.Severity,
                NoteText = segment.NoteText,
                SortOrder = segmentSortOrder++,
            });
        }

        for (var index = 0; index < aggregate.Suggestions.Count; index++)
        {
            var suggestion = aggregate.Suggestions[index];
            _dbContext.EvaluationSuggestions.Add(new EvaluationSuggestion
            {
                EvaluationDbId = evaluationDbId,
                SuggestionType = suggestion.SuggestionType,
                Title = suggestion.Title,
                Content = suggestion.Content,
                SortOrder = index,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Evaluation> GetEvaluationByPublicIdAsync(string evaluationId, CancellationToken cancellationToken)
    {
        return await _dbContext.Evaluations
            .SingleOrDefaultAsync(item => item.EvaluationId == evaluationId, cancellationToken)
            ?? throw new InvalidOperationException("未找到对应的评估任务。");
    }

    private Task EnsureAccessAsync(Evaluation evaluation, int? userId, string? accessToken)
    {
        if (userId.HasValue && evaluation.UserId == userId.Value)
        {
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(accessToken)
            && !string.IsNullOrWhiteSpace(evaluation.AnonymousTokenHash)
            && _accessTokenService.ValidateToken(evaluation.AnonymousTokenHash, accessToken))
        {
            return Task.CompletedTask;
        }

        throw new UnauthorizedAccessException("没有权限访问该评估结果。");
    }

    private async Task<EvaluationReportResponse> BuildReportAsync(int evaluationDbId, CancellationToken cancellationToken)
    {
        var evaluation = await _dbContext.Evaluations
            .SingleAsync(item => item.Id == evaluationDbId, cancellationToken);
        var options = DeserializeOptions(evaluation.OptionsJson);

        var segments = await _dbContext.EvaluationSegments
            .Where(item => item.EvaluationDbId == evaluationDbId)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);

        var suggestions = await _dbContext.EvaluationSuggestions
            .Where(item => item.EvaluationDbId == evaluationDbId)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);

        var referenceFileName = string.Empty;
        if (evaluation.ReferenceMediaFileId.HasValue)
        {
            referenceFileName = await _dbContext.MediaFiles
                .Where(item => item.Id == evaluation.ReferenceMediaFileId.Value)
                .Select(item => item.FileName)
                .SingleOrDefaultAsync(cancellationToken)
                ?? string.Empty;
        }

        var pitchAnalysis = DeserializeJson(evaluation.PitchAnalysisJson, new PitchAnalysisDto());
        var rhythmAnalysis = DeserializeJson(evaluation.RhythmAnalysisJson, new RhythmAnalysisDto
        {
            ThresholdMs = options.RhythmThresholdMs,
        });
        var transposeBase = DeserializeJson(evaluation.TransposeBaseJson, new TransposeBaseDto
        {
            Summary = LocalizedText.TransposePending(options)
        });

        pitchAnalysis.Segments = segments
            .Where(item => item.MetricType == "pitch")
            .Select(MapSegment)
            .ToList();
        rhythmAnalysis.Segments = segments
            .Where(item => item.MetricType == "rhythm")
            .Select(MapSegment)
            .ToList();
        rhythmAnalysis.ThresholdMs = rhythmAnalysis.ThresholdMs <= 0 ? options.RhythmThresholdMs : rhythmAnalysis.ThresholdMs;

        var coverage = ResolveCombinedMetric(pitchAnalysis.Coverage, rhythmAnalysis.Coverage);
        var consistency = ResolveCombinedMetric(pitchAnalysis.Consistency, rhythmAnalysis.Consistency);

        return new EvaluationReportResponse
        {
            Summary = new EvaluationSummaryDto
            {
                AnalysisId = evaluation.EvaluationId,
                ReferenceFileName = referenceFileName,
                PerformanceTempoBpm = rhythmAnalysis.PerformanceTempoBpm ?? evaluation.DetectedTempoBpm,
                ReferenceTempoBpm = rhythmAnalysis.ReferenceTempoBpm,
                TotalScore = evaluation.TotalScore,
                Badge = evaluation.Badge,
                ScoringProfile = evaluation.ScoringProfile,
                PitchScore = evaluation.PitchScore,
                RhythmScore = evaluation.RhythmScore,
                Coverage = coverage,
                Consistency = consistency,
                MeanPitchDeviationCents = evaluation.MeanPitchDeviationCents ?? pitchAnalysis.MeanDeviationCents,
                PitchStatus = evaluation.PitchStatus,
                RhythmStatus = evaluation.RhythmStatus,
                FeedbackLanguage = options.FeedbackLanguage,
                SummaryText = evaluation.SummaryText,
                GeneratedAt = evaluation.FinishedAt ?? evaluation.UpdatedAt,
            },
            PitchAnalysis = pitchAnalysis,
            RhythmAnalysis = rhythmAnalysis,
            TransposeBase = transposeBase,
            Suggestions = suggestions
                .Select(item => new EvaluationSuggestionDto
                {
                    SuggestionType = item.SuggestionType,
                    Title = item.Title,
                    Content = item.Content,
                })
                .ToList(),
            Warnings = DeserializeWarnings(evaluation.WarningMessagesJson),
        };
    }

    private static double? ResolveCombinedMetric(double? pitchMetric, double? rhythmMetric)
    {
        if (pitchMetric.HasValue && rhythmMetric.HasValue)
        {
            return Math.Round((pitchMetric.Value + rhythmMetric.Value) / 2.0, 1);
        }

        if (pitchMetric.HasValue)
        {
            return Math.Round(pitchMetric.Value, 1);
        }

        if (rhythmMetric.HasValue)
        {
            return Math.Round(rhythmMetric.Value, 1);
        }

        return null;
    }

    private static EvaluationSegmentDto MapSegment(EvaluationSegment item)
    {
        return new EvaluationSegmentDto
        {
            MetricType = item.MetricType,
            StartMs = item.StartMs,
            EndMs = item.EndMs,
            Score = item.Score,
            DeviationValue = item.DeviationValue,
            DeviationUnit = item.DeviationUnit,
            Severity = item.Severity,
            NoteText = item.NoteText,
        };
    }

    private static EvaluationStatusResponse MapStatusResponse(Evaluation evaluation, EvaluationOptionsRequest options)
    {
        return new EvaluationStatusResponse
        {
            EvaluationId = evaluation.EvaluationId,
            Status = evaluation.Status,
            Progress = evaluation.Progress,
            ScoringProfile = evaluation.ScoringProfile,
            TotalScore = evaluation.TotalScore,
            PitchStatus = evaluation.PitchStatus,
            RhythmStatus = evaluation.RhythmStatus,
            FeedbackLanguage = options.FeedbackLanguage,
            ScoringModel = options.ScoringModel,
            Warnings = DeserializeWarnings(evaluation.WarningMessagesJson),
            ErrorMessage = string.IsNullOrWhiteSpace(evaluation.ErrorMessage) ? null : evaluation.ErrorMessage,
        };
    }

    private async Task MarkFailedAsync(Evaluation evaluation, string errorMessage, CancellationToken cancellationToken)
    {
        var options = DeserializeOptions(evaluation.OptionsJson);
        evaluation.Status = "failed";
        evaluation.Progress = 100;
        evaluation.ScoringProfile = "unavailable";
        evaluation.ErrorMessage = errorMessage;
        evaluation.Badge = LocalizedText.FailedBadge(options);
        evaluation.SummaryText = errorMessage;
        evaluation.PitchStatus = evaluation.AnalyzePitch ? "failed" : "skipped";
        evaluation.RhythmStatus = evaluation.AnalyzeRhythm ? "failed" : "skipped";
        evaluation.FinishedAt = DateTime.UtcNow;
        evaluation.UpdatedAt = DateTime.UtcNow;
        evaluation.TransposeBaseJson = JsonSerializer.Serialize(new TransposeBaseDto
        {
            Summary = LocalizedText.TransposeUnavailable(options)
        }, SerializerOptions);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PitchAnalysisDto BuildPitchAnalysisDto(PitchAnalysisResult result)
    {
        return new PitchAnalysisDto
        {
            HitRate25 = result.HitRate25,
            HitRate50 = result.HitRate50,
            MeanDeviationCents = result.MeanDeviationCents,
            Coverage = result.Coverage,
            Consistency = result.Consistency,
            ReferencePoints = result.ReferencePoints,
            PerformancePoints = result.PerformancePoints,
            DeviationPoints = result.DeviationPoints,
            Segments = result.Segments,
        };
    }

    private static RhythmAnalysisDto BuildRhythmAnalysisDto(RhythmEvaluationResult result)
    {
        return new RhythmAnalysisDto
        {
            ThresholdMs = result.ThresholdMs,
            PerformanceTempoBpm = result.PerformanceTempoBpm,
            ReferenceTempoBpm = result.ReferenceTempoBpm,
            Coverage = result.Coverage,
            Consistency = result.Consistency,
            AverageDeviationMs = result.AverageDeviationMs,
            SeverityCounts = result.SeverityCounts,
            Segments = result.Segments,
        };
    }

    private static TransposeBaseDto BuildTransposeBase(
        PitchAnalysisResult pitchResult,
        MediaFile? referenceMedia,
        EvaluationOptionsRequest options)
    {
        if (referenceMedia == null)
        {
            return new TransposeBaseDto
            {
                Summary = LocalizedText.TransposeNeedsReference(options)
            };
        }

        if (string.IsNullOrWhiteSpace(pitchResult.DetectedKey) || pitchResult.DetectedKey == "--")
        {
            return new TransposeBaseDto
            {
                Summary = LocalizedText.TransposeUnstable(options)
            };
        }

        return new TransposeBaseDto
        {
            DetectedKey = pitchResult.DetectedKey,
            DetectedMode = pitchResult.DetectedMode ?? "--",
            ReferenceMedianMidi = pitchResult.ReferenceMedianMidi,
            Summary = LocalizedText.TransposeDetected(
                options,
                pitchResult.DetectedKey,
                pitchResult.DetectedMode ?? "--")
        };
    }

    private static TransposeSuggestionResponse BuildTransposeSuggestion(
        TransposeBaseDto transposeBase,
        TransposeSuggestionRequest request,
        EvaluationOptionsRequest options)
    {
        var isEnglish = string.Equals(options.FeedbackLanguage, "en-US", StringComparison.OrdinalIgnoreCase);
        var sourceGender = NormalizeGender(request.SourceGender, "male");
        var targetGender = NormalizeGender(request.TargetGender, "female");

        if (string.IsNullOrWhiteSpace(transposeBase.DetectedKey)
            || transposeBase.DetectedKey == "--"
            || !transposeBase.ReferenceMedianMidi.HasValue)
        {
            return new TransposeSuggestionResponse
            {
                DetectedKey = transposeBase.DetectedKey,
                DetectedMode = transposeBase.DetectedMode,
                Title = isEnglish ? "Key Suggestion Unavailable" : "暂时无法生成变调建议",
                Summary = isEnglish
                    ? "The reference melody was not stable enough to identify a dependable key center."
                    : "参考旋律暂时不够稳定，暂时无法识别可信的调性中心。",
                Tips =
                {
                    isEnglish
                        ? "Try a cleaner reference vocal or a melody-forward demo track."
                        : "建议换一段主旋律更清晰的参考音频后再试。",
                    isEnglish
                        ? "If the reference contains heavy accompaniment, upload a clearer standard take."
                        : "如果参考音频伴奏较重，建议上传更干净的标准音频。"
                }
            };
        }

        var targetCenterMidi = targetGender == "female" ? 62.0 : 57.0;
        var recommendedSemitone = (int)Math.Round(targetCenterMidi - transposeBase.ReferenceMedianMidi.Value);
        recommendedSemitone = Math.Clamp(recommendedSemitone, -6, 6);
        var recommendedKey = TransposeKey(transposeBase.DetectedKey, recommendedSemitone);
        var noChange = recommendedSemitone == 0;

        return new TransposeSuggestionResponse
        {
            DetectedKey = transposeBase.DetectedKey,
            DetectedMode = transposeBase.DetectedMode,
            RecommendedSemitone = recommendedSemitone,
            RecommendedKey = recommendedKey,
            Title = noChange
                ? (isEnglish ? "Current Key Already Fits" : "当前调性已经比较合适")
                : (isEnglish ? "Suggested Key Shift" : "推荐变调建议"),
            Summary = BuildTransposeSummary(
                transposeBase,
                sourceGender,
                targetGender,
                recommendedSemitone,
                recommendedKey,
                isEnglish),
            Tips = BuildTransposeTips(sourceGender, targetGender, recommendedSemitone, isEnglish),
        };
    }

    private static string BuildTransposeSummary(
        TransposeBaseDto transposeBase,
        string sourceGender,
        string targetGender,
        int recommendedSemitone,
        string recommendedKey,
        bool isEnglish)
    {
        if (recommendedSemitone == 0)
        {
            return isEnglish
                ? $"The detected key is {transposeBase.DetectedKey} {transposeBase.DetectedMode}. The current tessitura already fits the selected voice line."
                : $"检测到当前调性为 {transposeBase.DetectedKey} {FormatMode(transposeBase.DetectedMode)}，当前音域与目标声线已经比较匹配。";
        }

        var directionText = recommendedSemitone > 0
            ? (isEnglish ? $"raise by {recommendedSemitone} semitone(s)" : $"升 {recommendedSemitone} 个半音")
            : (isEnglish ? $"lower by {Math.Abs(recommendedSemitone)} semitone(s)" : $"降 {Math.Abs(recommendedSemitone)} 个半音");

        return isEnglish
            ? $"The detected key is {transposeBase.DetectedKey} {transposeBase.DetectedMode}. To move from the {sourceGender} line toward the {targetGender} line, {directionText} to {recommendedKey}."
            : $"检测到当前调性为 {transposeBase.DetectedKey} {FormatMode(transposeBase.DetectedMode)}，若从{FormatGender(sourceGender, false)}切换到{FormatGender(targetGender, false)}，建议{directionText}到 {recommendedKey}。";
    }

    private static List<string> BuildTransposeTips(string sourceGender, string targetGender, int recommendedSemitone, bool isEnglish)
    {
        var tips = new List<string>();
        if (recommendedSemitone == 0)
        {
            tips.Add(isEnglish
                ? "Keep the current key and focus on phrasing and resonance rather than transposition."
                : "建议保持当前调性，把重点放在共鸣位置和乐句表达上。");
        }
        else
        {
            tips.Add(isEnglish
                ? "Test the new key on the chorus first to verify the high notes stay comfortable."
                : "建议先用副歌高点试唱新调性，确认高音区是否更舒适。");
            tips.Add(isEnglish
                ? "If the timbre becomes too thin or too heavy, fine-tune by another semitone."
                : "如果变调后音色明显发薄或发闷，可以再微调 1 个半音。");
        }

        tips.Add(isEnglish
            ? $"Selected path: {sourceGender} -> {targetGender}."
            : $"当前建议路径：{FormatGender(sourceGender, true)} -> {FormatGender(targetGender, true)}。");
        return tips;
    }

    private static string NormalizeGender(string? value, string fallback)
    {
        if (string.Equals(value, "female", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "女声", StringComparison.OrdinalIgnoreCase))
        {
            return "female";
        }

        if (string.Equals(value, "male", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "男声", StringComparison.OrdinalIgnoreCase))
        {
            return "male";
        }

        return fallback;
    }

    private static string FormatGender(string gender, bool shortLabel)
    {
        return gender == "female"
            ? (shortLabel ? "女声" : "女声线")
            : (shortLabel ? "男声" : "男声线");
    }

    private static string FormatMode(string? mode)
    {
        return string.Equals(mode, "minor", StringComparison.OrdinalIgnoreCase) ? "小调" : "大调";
    }

    private static string TransposeKey(string detectedKey, int semitoneShift)
    {
        var keys = new[] { "C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" };
        var index = Array.IndexOf(keys, detectedKey);
        if (index < 0)
        {
            return detectedKey;
        }

        var shifted = (index + semitoneShift) % keys.Length;
        if (shifted < 0)
        {
            shifted += keys.Length;
        }

        return keys[shifted];
    }

    private static EvaluationOptionsRequest DeserializeOptions(string json)
    {
        return DeserializeJson(json, new EvaluationOptionsRequest()).Normalize();
    }

    private static T DeserializeJson<T>(string json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static List<string> DeserializeWarnings(string json)
    {
        return DeserializeJson(json, new List<string>());
    }

    private static string ResolveMediaType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp4" => "video",
            ".mov" => "video",
            _ => "audio"
        };
    }
}

internal static class LocalizedText
{
    public static string WaitingBadge(EvaluationOptionsRequest options)
    {
        return IsEnglish(options) ? "Pending" : "等待分析";
    }

    public static string FailedBadge(EvaluationOptionsRequest options)
    {
        return IsEnglish(options) ? "Failed" : "未完成";
    }

    public static string PendingSummary(EvaluationOptionsRequest options)
    {
        return IsEnglish(options)
            ? "The evaluation task has been created and is waiting to be processed."
            : "评估任务已创建，等待处理。";
    }

    public static string MissingPerformance(EvaluationOptionsRequest options)
    {
        return IsEnglish(options)
            ? "The performance source was not found, so the evaluation could not continue."
            : "未找到演唱素材，无法继续处理。";
    }

    public static string PerformancePreparationFailed(EvaluationOptionsRequest options)
    {
        return IsEnglish(options)
            ? "The performance source could not be prepared for analysis."
            : "演唱素材尚未准备完成。";
    }

    public static string ReferencePreparationFailed(EvaluationOptionsRequest options)
    {
        return IsEnglish(options)
            ? "The reference source could not be prepared and the task will fall back to a limited evaluation."
            : "参考素材准备失败，本次将回退为有限评估。";
    }

    public static string MissingReference(EvaluationOptionsRequest options)
    {
        return IsEnglish(options)
            ? "The reference source was not found, so the task fell back to a solo-performance evaluation."
            : "未找到参考素材，本次将回退为单素材评估。";
    }

    public static string PitchDisabled(EvaluationOptionsRequest options)
    {
        return IsEnglish(options) ? "Pitch analysis is disabled for this task." : "当前任务未启用音准分析。";
    }

    public static string RhythmDisabled(EvaluationOptionsRequest options)
    {
        return IsEnglish(options) ? "Rhythm analysis is disabled for this task." : "当前任务未启用节奏分析。";
    }

    public static string ExceptionPrefix(EvaluationOptionsRequest options)
    {
        return IsEnglish(options) ? "Evaluation pipeline error: " : "评估处理异常：";
    }

    public static string TransposePending(EvaluationOptionsRequest options)
    {
        return IsEnglish(options)
            ? "Complete an evaluation first to unlock key and transpose suggestions."
            : "先完成一次评估，系统才会生成调性识别和变调建议。";
    }

    public static string TransposeUnavailable(EvaluationOptionsRequest options)
    {
        return IsEnglish(options)
            ? "Transpose suggestions are unavailable because the evaluation did not finish successfully."
            : "由于评估未成功完成，当前无法生成变调建议。";
    }

    public static string TransposeNeedsReference(EvaluationOptionsRequest options)
    {
        return IsEnglish(options)
            ? "Upload a reference track to let the system identify the base key and prepare transpose advice."
            : "请先上传标准参考音频，系统才能识别基础调性并生成变调建议。";
    }

    public static string TransposeUnstable(EvaluationOptionsRequest options)
    {
        return IsEnglish(options)
            ? "The reference melody was not stable enough to identify a dependable key center."
            : "参考旋律暂时不够稳定，无法可靠识别当前调性。";
    }

    public static string TransposeDetected(EvaluationOptionsRequest options, string key, string mode)
    {
        return IsEnglish(options)
            ? $"The current reference key is detected as {key} {mode}."
            : $"系统识别当前标准音频的调性为 {key}{(mode == "minor" ? " 小调" : " 大调")}。";
    }

    private static bool IsEnglish(EvaluationOptionsRequest options)
    {
        return string.Equals(options.FeedbackLanguage, "en-US", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class MinimalPdfWriter
{
    public static byte[] Write(EvaluationReportResponse report, string evaluationId)
    {
        var lines = new List<string>
        {
            "Singing Evaluation Report",
            $"Evaluation: {evaluationId}",
            $"Generated: {report.Summary.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC",
            $"Total Score: {FormatNullable(report.Summary.TotalScore)}",
            $"Badge: {Sanitize(report.Summary.Badge)}",
            $"Profile: {Sanitize(report.Summary.ScoringProfile)}",
            $"Pitch Score: {FormatNullable(report.Summary.PitchScore)}",
            $"Rhythm Score: {FormatNullable(report.Summary.RhythmScore)}",
            $"User BPM: {FormatNullable(report.Summary.PerformanceTempoBpm)}",
            $"Reference BPM: {FormatNullable(report.Summary.ReferenceTempoBpm)}",
            $"Coverage: {FormatNullable(report.Summary.Coverage)}",
            $"Consistency: {FormatNullable(report.Summary.Consistency)}",
            $"Pitch Mean Cents: {FormatNullable(report.Summary.MeanPitchDeviationCents)}",
            $"Summary: {Sanitize(report.Summary.SummaryText)}",
        };

        foreach (var warning in report.Warnings.Take(3))
        {
            lines.Add($"Warning: {Sanitize(warning)}");
        }

        foreach (var suggestion in report.Suggestions.Take(4))
        {
            lines.Add($"Suggestion: {Sanitize(suggestion.Title)} - {Sanitize(suggestion.Content)}");
        }

        lines.Add($"Detected Key: {Sanitize(report.TransposeBase.DetectedKey)} {Sanitize(report.TransposeBase.DetectedMode)}");
        lines.Add($"Transpose Base: {Sanitize(report.TransposeBase.Summary)}");

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 18 Tf");
        contentBuilder.AppendLine("72 760 Td");
        contentBuilder.AppendLine("(Singing Evaluation Report) Tj");
        contentBuilder.AppendLine("0 -24 Td");
        contentBuilder.AppendLine("/F1 11 Tf");

        foreach (var line in lines.Skip(1))
        {
            contentBuilder.AppendLine($"({EscapePdfString(line)}) Tj");
            contentBuilder.AppendLine("0 -16 Td");
        }

        contentBuilder.AppendLine("ET");
        var content = Encoding.ASCII.GetBytes(contentBuilder.ToString());

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);

        var offsets = new List<long>();

        writer.WriteLine("%PDF-1.4");
        writer.Flush();
        offsets.Add(stream.Position);
        writer.WriteLine("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj");
        writer.Flush();
        offsets.Add(stream.Position);
        writer.WriteLine("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj");
        writer.Flush();
        offsets.Add(stream.Position);
        writer.WriteLine("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >> endobj");
        writer.Flush();
        offsets.Add(stream.Position);
        writer.WriteLine($"4 0 obj << /Length {content.Length} >> stream");
        writer.Flush();
        stream.Write(content, 0, content.Length);
        writer.WriteLine();
        writer.WriteLine("endstream endobj");
        writer.Flush();
        offsets.Add(stream.Position);
        writer.WriteLine("5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj");
        writer.Flush();

        var xrefPosition = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine("0 6");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets)
        {
            writer.WriteLine(offset.ToString("0000000000") + " 00000 n ");
        }

        writer.WriteLine("trailer << /Size 6 /Root 1 0 R >>");
        writer.WriteLine($"startxref {xrefPosition}");
        writer.WriteLine("%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static string FormatNullable(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.0") : "N/A";
    }

    private static string Sanitize(string value)
    {
        return new string(value.Select(character => character <= 127 ? character : '?').ToArray());
    }

    private static string EscapePdfString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
