using Microsoft.Extensions.Logging;

namespace Linkfire.MusicLibrary.Application.Catalog;

/// <summary>
/// Fans a search out to every registered catalogue and merges the answers. Adding Spotify or a third
/// provider is a new leaf plus a registration; nothing that consumes <see cref="IMusicCatalogProvider"/> changes.
/// </summary>
public sealed class CompositeMusicCatalogProvider : IMusicCatalogProvider
{
    public const string Name = "all";

    private readonly IReadOnlyList<IMusicCatalogProvider> _providers;
    private readonly ILogger<CompositeMusicCatalogProvider> _logger;

    public CompositeMusicCatalogProvider(
        IEnumerable<IMusicCatalogProvider> providers,
        ILogger<CompositeMusicCatalogProvider> logger)
    {
        _providers = providers.ToList();
        _logger = logger;

        if (_providers.Count == 0)
        {
            throw new ArgumentException("At least one catalogue provider must be registered.", nameof(providers));
        }
    }

    public string ProviderName => Name;

    public async Task<CatalogSearchResult> SearchAlbumsAsync(AlbumSearchQuery query, CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(_providers.Select(p => SearchOneAsync(p, query, cancellationToken)));

        return CatalogSearchResult.Merge(results);
    }

    private async Task<CatalogSearchResult> SearchOneAsync(
        IMusicCatalogProvider provider,
        AlbumSearchQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.SearchAlbumsAsync(query, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Providers are expected to report failures in their result. This is the isolation boundary
            // for the ones that do not: a bug in one adapter must not fail the whole search.
            _logger.LogError(ex, "Catalogue provider {Provider} threw during search", provider.ProviderName);
            return CatalogSearchResult.Failed(provider.ProviderName);
        }
    }
}
