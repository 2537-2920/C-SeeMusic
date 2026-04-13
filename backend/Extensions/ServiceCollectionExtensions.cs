using backend.Services;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMediaService, MediaService>();
        return services;
    }
}
