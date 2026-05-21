using backend.Services;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IBeatAnalysisService, BeatAnalysisService>();
        services.AddScoped<IPitchAnalysisService, PitchAnalysisService>();
        services.AddScoped<IRhythmEvaluationService, RhythmEvaluationService>();
        services.AddScoped<IEvaluationScoringService, EvaluationScoringService>();
        services.AddScoped<ITemporaryAudioPreparationService, TemporaryAudioPreparationService>();
        services.AddScoped<IInstantSingingEvaluationService, InstantSingingEvaluationService>();
        services.AddScoped<ITransposeSuggestionService, InstantTransposeSuggestionService>();
        services.AddScoped<IPdfExportService, PdfExportService>();
        services.AddScoped<IPianoTranscriptionService, PianoTranscriptionService>();
        services.AddScoped<IInstantTranscriptionService, InstantTranscriptionService>();
        services.AddScoped<IMediaService, NoDatabaseMediaService>();
        services.AddScoped<ITranscriptionService, NoDatabaseTranscriptionService>();
        services.AddScoped<ICommunityService, NoDatabaseCommunityService>();
        return services;
    }

    public static IServiceCollection AddDatabaseBackedApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IAudioPreparationService, AudioPreparationService>();
        services.AddScoped<ITranscriptionService, TranscriptionService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddSingleton<ITranscriptionTaskQueue, TranscriptionTaskQueue>();
        services.AddSingleton<IEvaluationTaskQueue, EvaluationTaskQueue>();
        services.AddSingleton<IAnonymousEvaluationAccessTokenService, AnonymousEvaluationAccessTokenService>();
        services.AddHostedService<TranscriptionWorker>();
        services.AddHostedService<EvaluationWorker>();
        services.AddScoped<ICommunityService, CommunityService>();
        return services;
    }
}
