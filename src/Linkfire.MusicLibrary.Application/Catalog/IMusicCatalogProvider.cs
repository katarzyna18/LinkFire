using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.Application.Catalog;

/// <summary>
/// Boundary to an external music catalogue (Deezer, Spotify, ...).
/// Implementations own all provider-specific contracts and translate them into <see cref="CatalogAlbum"/>.
/// </summary>
public interface IMusicCatalogProvider
{
    /// <summary>Stable identifier stored with saved albums, e.g. "deezer".</summary>
    string ProviderName { get; }

    /// <exception cref="MusicCatalogUnavailableException">
    /// Thrown when the provider cannot be reached or returns an unusable response.
    /// </exception>
    Task<IReadOnlyList<CatalogAlbum>> SearchAlbumsAsync(AlbumSearchQuery query, CancellationToken cancellationToken);
}
