using backend.Services;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IBeatAnalysisService, BeatAnalysisService>();
        services.AddScoped<IAudioPreparationService, AudioPreparationService>();
        services.AddScoped<IPitchAnalysisService, PitchAnalysisService>();
        services.AddScoped<IRhythmEvaluationService, RhythmEvaluationService>();
        services.AddScoped<IEvaluationScoringService, EvaluationScoringService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddSingleton<IEvaluationTaskQueue, EvaluationTaskQueue>();
        services.AddSingleton<IAnonymousEvaluationAccessTokenService, AnonymousEvaluationAccessTokenService>();
        services.AddHostedService<EvaluationWorker>();
        return services;
    }
}
