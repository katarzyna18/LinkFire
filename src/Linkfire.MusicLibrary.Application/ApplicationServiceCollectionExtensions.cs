using Linkfire.MusicLibrary.Application.Libraries;
using Linkfire.MusicLibrary.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Linkfire.MusicLibrary.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<UserService>();
        services.AddScoped<LibraryService>();
        return services;
    }
}
