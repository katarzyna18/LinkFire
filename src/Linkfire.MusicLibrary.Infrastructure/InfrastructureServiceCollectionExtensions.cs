using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Application.Persistence;
using Linkfire.MusicLibrary.Infrastructure.Deezer;
using Linkfire.MusicLibrary.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Linkfire.MusicLibrary.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MusicLibrary")
            ?? throw new InvalidOperationException("Connection string 'MusicLibrary' is not configured.");

        services.AddDbContext<MusicLibraryDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IMusicLibraryDbContext>(sp => sp.GetRequiredService<MusicLibraryDbContext>());

        services.AddDeezerCatalogProvider(configuration);

        return services;
    }

    public static IServiceCollection AddDeezerCatalogProvider(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DeezerOptions>(configuration.GetSection(DeezerOptions.SectionName));

        services.AddHttpClient<IMusicCatalogProvider, DeezerMusicCatalogProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<DeezerOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}
