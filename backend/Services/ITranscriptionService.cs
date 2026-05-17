using backend.Models;

namespace backend.Services;

public interface ITranscriptionService
{
    Task<CreateTranscriptionResponse> CreateAsync(
        CreateTranscriptionRequest request,
        int? userId,
        CancellationToken cancellationToken = default);

    Task<TranscriptionStatusResponse> GetStatusAsync(
        string jobId,
        int? userId,
        CancellationToken cancellationToken = default);

    Task<ScoreDetailResponse> GetScoreAsync(
        string scoreId,
        int? userId,
        CancellationToken cancellationToken = default);

    Task<TranscriptionResult> AnalyzeLegacyAsync(
        TranscriptionRequest request,
        int? userId,
        CancellationToken cancellationToken = default);

    Task ProcessQueuedTranscriptionAsync(int transcriptionJobDbId, CancellationToken cancellationToken = default);
}

public interface ITranscriptionTaskQueue
{
    ValueTask QueueAsync(int transcriptionJobDbId, CancellationToken cancellationToken = default);
    ValueTask<int> DequeueAsync(CancellationToken cancellationToken = default);
}

public interface IPianoTranscriptionService
{
    PianoTranscriptionResult Transcribe(string preparedAudioPath, string title, TranscriptionOptionsRequest options);
}

public sealed class TranscriptionProcessingOptions
{
    public int ImmediateProcessingMaxDurationSeconds { get; set; } = 45;
    public int MeasuresPerSystem { get; set; } = 6;
    public int SystemsPerPage { get; set; } = 2;
}

public sealed class TranscriptionExecutionPlanner
{
    private readonly TranscriptionProcessingOptions _options;

    public TranscriptionExecutionPlanner(TranscriptionProcessingOptions options)
    {
        _options = options;
    }

    public bool ShouldProcessSynchronously(MediaFile mediaFile)
    {
        if (!string.Equals(mediaFile.PreparedAudioStatus, "ready", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (mediaFile.DurationMs is null)
        {
            return false;
        }

        return mediaFile.DurationMs.Value <= _options.ImmediateProcessingMaxDurationSeconds * 1000;
    }
}

public sealed class PianoTranscriptionResult
{
    public string Status { get; set; } = "failed";
    public BeatAnalysisResult BeatAnalysis { get; set; } = new();
    public string KeySignature { get; set; } = "C";
    public double KeyConfidence { get; set; }
    public string MusicXmlContent { get; set; } = string.Empty;
    public int MeasureCount { get; set; }
    public int EstimatedPageCount { get; set; }
    public string PreviewRenderMode { get; set; } = string.Empty;
    public string TrackBuildMode { get; set; } = string.Empty;
    public ScoreAnalysisSummaryResponse AnalysisSummary { get; set; } = new();
    public List<GeneratedTrackResult> Tracks { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class GeneratedTrackResult
{
    public string Name { get; set; } = string.Empty;
    public string HandRole { get; set; } = string.Empty;
    public string Instrument { get; set; } = "piano";
    public bool IsGenerated { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public List<GeneratedNoteResult> Notes { get; set; } = new();
}

public sealed class GeneratedNoteResult
{
    public int MeasureNo { get; set; }
    public double BeatStart { get; set; }
    public string DurationType { get; set; } = "quarter";
    public double DurationBeats { get; set; }
    public string PitchName { get; set; } = string.Empty;
    public int MidiNumber { get; set; }
    public string Staff { get; set; } = string.Empty;
    public double StartTimeSeconds { get; set; }
    public bool IsChordTone { get; set; }
}
