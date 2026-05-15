using System.Text.Json;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class TranscriptionService : ITranscriptionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SeeMusicDbContext _dbContext;
    private readonly IAudioPreparationService _audioPreparationService;
    private readonly IPianoTranscriptionService _pianoTranscriptionService;
    private readonly ITranscriptionTaskQueue _taskQueue;
    private readonly TranscriptionExecutionPlanner _executionPlanner;
    private readonly TranscriptionProcessingOptions _processingOptions;

    public TranscriptionService(
        SeeMusicDbContext dbContext,
        IAudioPreparationService audioPreparationService,
        IPianoTranscriptionService pianoTranscriptionService,
        ITranscriptionTaskQueue taskQueue,
        IOptions<TranscriptionProcessingOptions> processingOptions)
    {
        _dbContext = dbContext;
        _audioPreparationService = audioPreparationService;
        _pianoTranscriptionService = pianoTranscriptionService;
        _taskQueue = taskQueue;
        _processingOptions = processingOptions.Value ?? new TranscriptionProcessingOptions();
        _executionPlanner = new TranscriptionExecutionPlanner(_processingOptions);
    }

    public async Task<CreateTranscriptionResponse> CreateAsync(
        CreateTranscriptionRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.MediaId))
        {
            throw new InvalidOperationException("缺少媒体标识，无法创建识谱任务。");
        }

        var options = (request.Options ?? new TranscriptionOptionsRequest()).Normalize();
        if (!string.Equals(options.Mode, "piano", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("当前版本仅支持钢琴识谱。");
        }

        var media = await _dbContext.MediaFiles.SingleOrDefaultAsync(item => item.MediaId == request.MediaId, cancellationToken);
        if (media == null)
        {
            throw new InvalidOperationException("未找到对应的音频文件。");
        }

        var title = string.IsNullOrWhiteSpace(request.ProjectTitle)
            ? Path.GetFileNameWithoutExtension(media.FileName)
            : request.ProjectTitle.Trim();

        var job = new TranscriptionJob
        {
            JobId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            SourceMediaFileId = media.Id,
            ProjectTitle = string.IsNullOrWhiteSpace(title) ? "我的智能识谱项目" : title,
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "audio" : request.SourceType.Trim().ToLowerInvariant(),
            Status = "queued",
            Progress = 0,
            OptionsJson = JsonSerializer.Serialize(options, JsonOptions),
            WarningMessagesJson = "[]",
            BeatAnalysisJson = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.TranscriptionJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_executionPlanner.ShouldProcessSynchronously(media))
        {
            await ProcessQueuedTranscriptionAsync(job.Id, cancellationToken);
            job = await _dbContext.TranscriptionJobs.SingleAsync(item => item.Id == job.Id, cancellationToken);
        }
        else
        {
            await _taskQueue.QueueAsync(job.Id, cancellationToken);
        }

        return new CreateTranscriptionResponse
        {
            JobId = job.JobId,
            Status = job.Status,
            Progress = job.Progress,
            ScoreId = await ResolveScorePublicIdAsync(job.ScoreDbId, cancellationToken),
            Message = BuildJobMessage(job)
        };
    }

    public async Task<TranscriptionStatusResponse> GetStatusAsync(
        string jobId,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.TranscriptionJobs.SingleOrDefaultAsync(item => item.JobId == jobId, cancellationToken);
        if (job == null)
        {
            throw new InvalidOperationException("未找到对应的识谱任务。");
        }

        var trackSummaries = job.ScoreDbId.HasValue
            ? await _dbContext.ScoreTracks
                .Where(item => item.ScoreDbId == job.ScoreDbId.Value)
                .OrderBy(item => item.SortOrder)
                .Select(item => new ScoreTrackResponse
                {
                    Name = item.Name,
                    HandRole = item.HandRole,
                    Instrument = item.Instrument,
                    NoteCount = item.NoteCount,
                    RangeLowMidi = item.RangeLowMidi,
                    RangeHighMidi = item.RangeHighMidi,
                    IsGenerated = item.IsGenerated,
                    SummaryText = item.SummaryText
                })
                .ToListAsync(cancellationToken)
            : new List<ScoreTrackResponse>();

        return new TranscriptionStatusResponse
        {
            JobId = job.JobId,
            Status = job.Status,
            Progress = job.Progress,
            ErrorMessage = string.IsNullOrWhiteSpace(job.ErrorMessage) ? null : job.ErrorMessage,
            ScoreId = await ResolveScorePublicIdAsync(job.ScoreDbId, cancellationToken),
            DetectedTempoBpm = job.DetectedTempoBpm,
            DetectedTimeSignature = job.DetectedTimeSignature,
            MeasureCount = job.MeasureCount,
            EstimatedPageCount = job.EstimatedPageCount,
            TrackSummaries = trackSummaries,
            Warnings = DeserializeList(job.WarningMessagesJson)
        };
    }

    public async Task<ScoreDetailResponse> GetScoreAsync(
        string scoreId,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var score = await _dbContext.Scores.SingleOrDefaultAsync(item => item.ScoreId == scoreId, cancellationToken);
        if (score == null)
        {
            throw new InvalidOperationException("未找到对应的乐谱结果。");
        }

        var trackEntities = await _dbContext.ScoreTracks
            .Where(item => item.ScoreDbId == score.Id)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var noteEntities = await _dbContext.ScoreNotes
            .Where(item => item.ScoreDbId == score.Id)
            .OrderBy(item => item.ScoreTrackDbId)
            .ThenBy(item => item.MeasureNo)
            .ThenBy(item => item.BeatStart)
            .ThenBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);

        var tracks = trackEntities
            .Select(item => new ScoreTrackResponse
            {
                Name = item.Name,
                HandRole = item.HandRole,
                Instrument = item.Instrument,
                NoteCount = item.NoteCount,
                RangeLowMidi = item.RangeLowMidi,
                RangeHighMidi = item.RangeHighMidi,
                IsGenerated = item.IsGenerated,
                SummaryText = item.SummaryText
            })
            .ToList();

        var previewTracks = trackEntities
            .Select(track => new PreviewTrack
            {
                HandRole = track.HandRole,
                Notes = noteEntities
                    .Where(note => note.ScoreTrackDbId == track.Id)
                    .Select(note => new PreviewNote
                    {
                        MeasureNo = note.MeasureNo,
                        BeatStart = note.BeatStart,
                        DurationType = note.DurationType,
                        PitchName = note.PitchName
                    })
                    .ToList()
            })
            .ToList();

        return new ScoreDetailResponse
        {
            ScoreId = score.ScoreId,
            Title = score.Title,
            InstrumentMode = score.InstrumentMode,
            Status = score.Status,
            TempoBpm = score.TempoBpm,
            TimeSignature = score.TimeSignature,
            KeySignature = score.KeySignature,
            MeasureCount = score.MeasureCount,
            EstimatedPageCount = score.EstimatedPageCount,
            MusicXmlContent = score.MusicXmlContent,
            Tracks = tracks,
            AnalysisSummary = DeserializeAnalysisSummary(score.AnalysisSummaryJson),
            PreviewPages = ScorePreviewRenderer.RenderPages(
                score.Title,
                score.TempoBpm,
                score.TimeSignature,
                score.KeySignature,
                score.MeasureCount,
                _processingOptions.MeasuresPerSystem,
                _processingOptions.SystemsPerPage,
                previewTracks),
            Warnings = DeserializeList(score.WarningMessagesJson)
        };
    }

    public async Task<TranscriptionResult> AnalyzeLegacyAsync(
        TranscriptionRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.MediaId))
        {
            return CreateLegacyFailure(string.Empty, "缺少音频标识，无法执行识谱。");
        }

        var media = await _dbContext.MediaFiles.SingleOrDefaultAsync(item => item.MediaId == request.MediaId, cancellationToken);
        if (media == null)
        {
            return CreateLegacyFailure(request.MediaId, "未找到对应的音频文件。");
        }

        var createResponse = await CreateAsync(
            new CreateTranscriptionRequest
            {
                MediaId = request.MediaId,
                SourceType = "audio",
                ProjectTitle = Path.GetFileNameWithoutExtension(media.FileName),
                Options = new TranscriptionOptionsRequest
                {
                    Mode = "piano",
                    SeparateMelody = request.SeparateMelody,
                    SeparateAccompaniment = request.SeparateAccompaniment,
                    AnalyzeRhythm = true
                }
            },
            userId,
            cancellationToken);

        var job = await _dbContext.TranscriptionJobs.SingleAsync(item => item.JobId == createResponse.JobId, cancellationToken);
        var beatAnalysis = DeserializeBeatAnalysis(job.BeatAnalysisJson);
        if (!beatAnalysis.IsAvailable && !string.IsNullOrWhiteSpace(job.ErrorMessage))
        {
            beatAnalysis.Summary = job.ErrorMessage;
        }

        return new TranscriptionResult
        {
            MediaId = request.MediaId,
            Status = job.Status,
            ScoreId = createResponse.ScoreId ?? string.Empty,
            Message = createResponse.Message,
            BeatAnalysis = beatAnalysis
        };
    }

    public async Task ProcessQueuedTranscriptionAsync(int transcriptionJobDbId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.TranscriptionJobs.SingleOrDefaultAsync(item => item.Id == transcriptionJobDbId, cancellationToken);
        if (job == null)
        {
            return;
        }

        if (string.Equals(job.Status, "succeeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(job.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var media = await _dbContext.MediaFiles.SingleAsync(item => item.Id == job.SourceMediaFileId, cancellationToken);
        var options = DeserializeOptions(job.OptionsJson);

        try
        {
            job.Status = "processing";
            job.Progress = 10;
            job.ErrorMessage = string.Empty;
            job.StartedAt ??= DateTime.UtcNow;
            job.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var prepared = await _audioPreparationService.PrepareAsync(media, cancellationToken);
            if (!string.Equals(prepared.Status, "ready", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(prepared.AbsolutePath))
            {
                FailJob(job, prepared.ErrorMessage ?? "音频预处理失败，无法识谱。");
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            job.Progress = 35;
            job.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var result = _pianoTranscriptionService.Transcribe(prepared.AbsolutePath, job.ProjectTitle, options);
            job.BeatAnalysisJson = JsonSerializer.Serialize(result.BeatAnalysis, JsonOptions);
            job.WarningMessagesJson = JsonSerializer.Serialize(result.Warnings, JsonOptions);

            if (!string.Equals(result.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                FailJob(job, string.IsNullOrWhiteSpace(result.ErrorMessage) ? "识谱失败。" : result.ErrorMessage);
                job.DetectedTempoBpm = result.BeatAnalysis.TempoBpm > 0 ? result.BeatAnalysis.TempoBpm : null;
                job.DetectedTimeSignature = result.BeatAnalysis.TimeSignatureNumerator > 0
                    ? $"{result.BeatAnalysis.TimeSignatureNumerator}/{Math.Max(1, result.BeatAnalysis.TimeSignatureDenominator)}"
                    : null;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            job.Progress = 70;
            job.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var score = new Score
            {
                ScoreId = Guid.NewGuid().ToString("N"),
                UserId = job.UserId,
                SourceMediaFileId = media.Id,
                Title = job.ProjectTitle,
                InstrumentMode = "piano",
                Status = "ready",
                TempoBpm = result.BeatAnalysis.TempoBpm > 0 ? result.BeatAnalysis.TempoBpm : null,
                TimeSignature = $"{Math.Max(1, result.BeatAnalysis.TimeSignatureNumerator)}/{Math.Max(1, result.BeatAnalysis.TimeSignatureDenominator)}",
                KeySignature = result.KeySignature,
                MeasureCount = result.MeasureCount,
                EstimatedPageCount = result.EstimatedPageCount,
                MusicXmlContent = result.MusicXmlContent,
                AnalysisSummaryJson = JsonSerializer.Serialize(result.AnalysisSummary, JsonOptions),
                WarningMessagesJson = JsonSerializer.Serialize(result.Warnings, JsonOptions),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Scores.Add(score);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var trackEntities = new List<ScoreTrack>();
            foreach (var track in result.Tracks.Select((track, index) => new { Track = track, Index = index }))
            {
                var noteCount = track.Track.Notes.Count;
                int? rangeLow = track.Track.Notes.Count == 0 ? null : track.Track.Notes.Min(note => note.MidiNumber);
                int? rangeHigh = track.Track.Notes.Count == 0 ? null : track.Track.Notes.Max(note => note.MidiNumber);
                trackEntities.Add(new ScoreTrack
                {
                    ScoreDbId = score.Id,
                    Name = track.Track.Name,
                    HandRole = track.Track.HandRole,
                    Instrument = track.Track.Instrument,
                    NoteCount = noteCount,
                    RangeLowMidi = rangeLow,
                    RangeHighMidi = rangeHigh,
                    IsGenerated = track.Track.IsGenerated,
                    SummaryText = track.Track.SummaryText,
                    SortOrder = track.Index,
                });
            }

            _dbContext.ScoreTracks.AddRange(trackEntities);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var noteEntities = new List<ScoreNote>();
            foreach (var track in result.Tracks)
            {
                var trackEntity = trackEntities.Single(item => item.HandRole == track.HandRole);
                for (var noteIndex = 0; noteIndex < track.Notes.Count; noteIndex++)
                {
                    var note = track.Notes[noteIndex];
                    noteEntities.Add(new ScoreNote
                    {
                        ScoreDbId = score.Id,
                        ScoreTrackDbId = trackEntity.Id,
                        MeasureNo = note.MeasureNo,
                        BeatStart = note.BeatStart,
                        DurationType = note.DurationType,
                        DurationBeats = note.DurationBeats,
                        PitchName = note.PitchName,
                        MidiNumber = note.MidiNumber,
                        Staff = note.Staff,
                        StartTimeSeconds = note.StartTimeSeconds,
                        IsChordTone = note.IsChordTone,
                        SortOrder = noteIndex
                    });
                }
            }

            _dbContext.ScoreNotes.AddRange(noteEntities);
            await _dbContext.SaveChangesAsync(cancellationToken);

            job.Status = "succeeded";
            job.Progress = 100;
            job.ScoreDbId = score.Id;
            job.DetectedTempoBpm = score.TempoBpm;
            job.DetectedTimeSignature = score.TimeSignature;
            job.MeasureCount = score.MeasureCount;
            job.EstimatedPageCount = score.EstimatedPageCount;
            job.ErrorMessage = string.Empty;
            job.FinishedAt = DateTime.UtcNow;
            job.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            FailJob(job, exception.Message);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string BuildJobMessage(TranscriptionJob job)
    {
        return job.Status switch
        {
            "succeeded" => "识谱完成，已生成双手钢琴谱预览。",
            "failed" => string.IsNullOrWhiteSpace(job.ErrorMessage) ? "识谱失败。" : job.ErrorMessage,
            "processing" => "识谱任务正在处理中。",
            _ => "识谱任务已进入队列，请稍后刷新结果。"
        };
    }

    private static void FailJob(TranscriptionJob job, string message)
    {
        job.Status = "failed";
        job.Progress = 100;
        job.ErrorMessage = message;
        job.FinishedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<string?> ResolveScorePublicIdAsync(int? scoreDbId, CancellationToken cancellationToken)
    {
        if (!scoreDbId.HasValue)
        {
            return null;
        }

        return await _dbContext.Scores
            .Where(item => item.Id == scoreDbId.Value)
            .Select(item => item.ScoreId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static TranscriptionOptionsRequest DeserializeOptions(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TranscriptionOptionsRequest();
        }

        return JsonSerializer.Deserialize<TranscriptionOptionsRequest>(json, JsonOptions)?.Normalize()
            ?? new TranscriptionOptionsRequest();
    }

    private static List<string> DeserializeList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
    }

    private static ScoreAnalysisSummaryResponse DeserializeAnalysisSummary(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ScoreAnalysisSummaryResponse();
        }

        return JsonSerializer.Deserialize<ScoreAnalysisSummaryResponse>(json, JsonOptions)
            ?? new ScoreAnalysisSummaryResponse();
    }

    private static BeatAnalysisResult DeserializeBeatAnalysis(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new BeatAnalysisResult();
        }

        return JsonSerializer.Deserialize<BeatAnalysisResult>(json, JsonOptions) ?? new BeatAnalysisResult();
    }

    private static TranscriptionResult CreateLegacyFailure(string mediaId, string message)
    {
        return new TranscriptionResult
        {
            MediaId = mediaId,
            Status = "failed",
            ScoreId = string.Empty,
            Message = message,
            BeatAnalysis = new BeatAnalysisResult
            {
                IsAvailable = false,
                Summary = message
            }
        };
    }
}
