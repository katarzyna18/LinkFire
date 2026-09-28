using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Application.Libraries;
using Linkfire.MusicLibrary.Application.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Linkfire.MusicLibrary.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<UserService>();
        services.AddScoped<LibraryService>();

        // Consumers ask for one IMusicCatalogProvider and get every registered catalogue behind it.
        services.AddTransient<IMusicCatalogProvider>(sp => new CompositeMusicCatalogProvider(
            sp.GetKeyedServices<IMusicCatalogProvider>(MusicCatalogProviders.LeafServiceKey),
            sp.GetRequiredService<ILogger<CompositeMusicCatalogProvider>>()));

        return services;
    }
}
