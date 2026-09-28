using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.ApiTests;

public sealed class FakeMusicCatalogProvider : IMusicCatalogProvider
{
    public string ProviderName => "fake";

    public IReadOnlyList<CatalogAlbum> Results { get; set; } = [];

    public bool Unavailable { get; set; }

    public AlbumSearchQuery? LastQuery { get; private set; }

    public Task<IReadOnlyList<CatalogAlbum>> SearchAlbumsAsync(AlbumSearchQuery query, CancellationToken cancellationToken)
    {
        LastQuery = query;

        if (Unavailable)
        {
            throw new MusicCatalogUnavailableException(ProviderName, "Simulated outage.");
        }

        return Task.FromResult(Results);
    }
}
