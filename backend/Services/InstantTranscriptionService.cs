using backend.Models;
using Microsoft.Extensions.Options;

namespace backend.Services;

public interface IInstantTranscriptionService
{
    Task<ScoreDetailResponse> TranscribeAsync(
        IFormFile audioFile,
        string title,
        TranscriptionOptionsRequest options,
        CancellationToken cancellationToken = default);
}

public sealed class InstantTranscriptionService : IInstantTranscriptionService
{
    private readonly ITemporaryAudioPreparationService _temporaryAudioPreparationService;
    private readonly IPianoTranscriptionService _pianoTranscriptionService;
    private readonly TranscriptionProcessingOptions _processingOptions;

    public InstantTranscriptionService(
        ITemporaryAudioPreparationService temporaryAudioPreparationService,
        IPianoTranscriptionService pianoTranscriptionService,
        IOptions<TranscriptionProcessingOptions> processingOptions)
    {
        _temporaryAudioPreparationService = temporaryAudioPreparationService;
        _pianoTranscriptionService = pianoTranscriptionService;
        _processingOptions = processingOptions.Value ?? new TranscriptionProcessingOptions();
    }

    public async Task<ScoreDetailResponse> TranscribeAsync(
        IFormFile audioFile,
        string title,
        TranscriptionOptionsRequest options,
        CancellationToken cancellationToken = default)
    {
        if (audioFile == null || audioFile.Length == 0)
        {
            throw new InvalidOperationException("请先选择可用的本地音频文件。");
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(audioFile.FileName)
            : title.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            normalizedTitle = "我的智能识谱项目";
        }

        var prepared = await _temporaryAudioPreparationService.PrepareAsync(audioFile, cancellationToken);
        var cleanupDir = prepared.WorkingDirectory;

        try
        {
            if (!string.Equals(prepared.Status, "ready", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(prepared.AbsolutePath))
            {
                throw new InvalidOperationException(prepared.ErrorMessage ?? "音频预处理失败，无法识谱。");
            }

            var result = _pianoTranscriptionService.Transcribe(
                prepared.AbsolutePath,
                normalizedTitle,
                options ?? new TranscriptionOptionsRequest());

            if (!string.Equals(result.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "识谱失败，请尝试使用旋律更清晰的音频。"
                    : result.ErrorMessage);
            }

            return BuildScoreResponse(result, normalizedTitle);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(cleanupDir) && Directory.Exists(cleanupDir))
            {
                try { Directory.Delete(cleanupDir, true); } catch { }
            }
        }
    }

    private ScoreDetailResponse BuildScoreResponse(PianoTranscriptionResult result, string title)
    {
        var timeSignature = result.BeatAnalysis.TimeSignatureNumerator > 0
            ? $"{result.BeatAnalysis.TimeSignatureNumerator}/{Math.Max(1, result.BeatAnalysis.TimeSignatureDenominator)}"
            : "4/4";

        var tracks = result.Tracks.Select(track => new ScoreTrackResponse
        {
            Name = track.Name,
            HandRole = track.HandRole,
            Instrument = track.Instrument,
            NoteCount = track.Notes.Count,
            RangeLowMidi = track.Notes.Count == 0 ? null : track.Notes.Min(n => n.MidiNumber),
            RangeHighMidi = track.Notes.Count == 0 ? null : track.Notes.Max(n => n.MidiNumber),
            IsGenerated = track.IsGenerated,
            Origin = track.Origin,
            SummaryText = track.SummaryText
        }).ToList();

        double? tempoBpm = result.BeatAnalysis.TempoBpm > 0 ? result.BeatAnalysis.TempoBpm : null;

        return new ScoreDetailResponse
        {
            ScoreId = Guid.NewGuid().ToString("N"),
            Title = title,
            InstrumentMode = "piano",
            Status = "ready",
            TempoBpm = tempoBpm,
            TimeSignature = timeSignature,
            KeySignature = result.KeySignature,
            KeyConfidence = result.KeyConfidence > 0 ? result.KeyConfidence : null,
            TimeSignatureConfidence = result.BeatAnalysis.TimeSignatureConfidence > 0
                ? result.BeatAnalysis.TimeSignatureConfidence
                : null,
            MeasureCount = result.MeasureCount,
            EstimatedPageCount = result.EstimatedPageCount,
            MusicXmlContent = result.MusicXmlContent,
            PreviewRenderMode = "musicxml",
            TrackBuildMode = result.TrackBuildMode,
            RhythmGridSource = result.BeatAnalysis.GridSource ?? string.Empty,
            Tracks = tracks,
            AnalysisSummary = result.AnalysisSummary,
            PreviewPages = new List<ScorePreviewPageResponse>(),
            Warnings = result.Warnings
        };
    }
}
