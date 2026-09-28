namespace Linkfire.MusicLibrary.Application.Catalog;

/// <summary>
/// Boundary to an external music catalogue (Deezer, Spotify, ...).
/// Implementations own all provider-specific contracts and translate them into <see cref="CatalogSearchResult"/>.
/// Provider failures (network, timeout, rate limit, unusable payload) are reported through the result,
/// never thrown, so one failing provider cannot take down a search that others can still answer.
/// </summary>
public interface IMusicCatalogProvider
{
    /// <summary>Stable identifier stored with saved albums, e.g. "deezer".</summary>
    string ProviderName { get; }

    Task<CatalogSearchResult> SearchAlbumsAsync(AlbumSearchQuery query, CancellationToken cancellationToken);
}
