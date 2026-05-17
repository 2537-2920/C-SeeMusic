using backend.Models;

namespace backend.Services;

public interface IInstantSingingEvaluationService
{
    Task<EvaluationReportResponse> EvaluateAsync(
        IFormFile performanceFile,
        IFormFile referenceFile,
        EvaluationOptionsRequest options,
        CancellationToken cancellationToken = default);
}

public interface ITemporaryAudioPreparationService
{
    Task<TemporaryPreparedAudioResult> PrepareAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);
}

public interface ITransposeSuggestionService
{
    TransposeSuggestionResponse Build(TransposeSuggestionRequest request);
}

public interface IPdfExportService
{
    byte[] Export(EvaluationReportResponse report);
}

public sealed class TemporaryPreparedAudioResult
{
    public string Status { get; set; } = "failed";
    public string? AbsolutePath { get; set; }
    public string? WorkingDirectory { get; set; }
    public int? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}
