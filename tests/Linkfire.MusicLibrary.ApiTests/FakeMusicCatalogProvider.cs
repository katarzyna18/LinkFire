using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.ApiTests;

public sealed class FakeMusicCatalogProvider : IMusicCatalogProvider
{
    public string ProviderName => "fake";

    public IReadOnlyList<CatalogAlbum> Results { get; set; } = [];

    public bool Unavailable { get; set; }

    public AlbumSearchQuery? LastQuery { get; private set; }

    public Task<CatalogSearchResult> SearchAlbumsAsync(AlbumSearchQuery query, CancellationToken cancellationToken)
    {
        LastQuery = query;

        return Task.FromResult(Unavailable
            ? CatalogSearchResult.Failed(ProviderName)
            : CatalogSearchResult.Success(ProviderName, Results));
    }
}
