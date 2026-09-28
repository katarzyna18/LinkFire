using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Application.Persistence;
using Linkfire.MusicLibrary.Infrastructure.Deezer;
using Linkfire.MusicLibrary.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
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

    /// <summary>
    /// Registers Deezer as one leaf catalogue. A Spotify adapter would be registered the same way and
    /// automatically picked up by the composite provider.
    /// </summary>
    public static IServiceCollection AddDeezerCatalogProvider(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DeezerOptions>()
            .Bind(configuration.GetSection(DeezerOptions.SectionName))
            .Validate(
                o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _)
                     && o.AttemptTimeoutSeconds > 0
                     && o.TotalTimeoutSeconds >= o.AttemptTimeoutSeconds
                     && o.MaxRetries is >= 0 and <= 5
                     && o.MaxResults is > 0 and <= 100,
                "Deezer options require an absolute BaseUrl, AttemptTimeoutSeconds > 0, TotalTimeoutSeconds >= AttemptTimeoutSeconds, MaxRetries 0-5 and MaxResults 1-100.")
            .ValidateOnStart();

        services.AddHttpClient<DeezerMusicCatalogProvider>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<DeezerOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                // The resilience pipeline owns timeouts; this is only a last-resort upper bound.
                client.Timeout = TimeSpan.FromSeconds(options.TotalTimeoutSeconds + 5);
            })
            .AddStandardResilienceHandler()
            .Configure((resilience, sp) =>
            {
                var options = sp.GetRequiredService<IOptions<DeezerOptions>>().Value;
                var attemptTimeout = TimeSpan.FromSeconds(options.AttemptTimeoutSeconds);

                // Retries are safe because search is an idempotent GET; the standard handler already
                // limits them to transient failures (5xx, 408, 429, network errors, attempt timeouts).
                resilience.AttemptTimeout.Timeout = attemptTimeout;
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(options.TotalTimeoutSeconds);
                resilience.Retry.MaxRetryAttempts = options.MaxRetries;
                resilience.Retry.UseJitter = true;
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromTicks(Math.Max(attemptTimeout.Ticks * 2, TimeSpan.FromSeconds(30).Ticks));
            });

        services.AddKeyedTransient<IMusicCatalogProvider>(
            MusicCatalogProviders.LeafServiceKey,
            (sp, _) => sp.GetRequiredService<DeezerMusicCatalogProvider>());

        return services;
    }
}
