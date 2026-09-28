using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.Api.Contracts;

/// <summary>
/// Search results plus the providers that could not answer. With several catalogues configured a
/// partial outage degrades the answer instead of failing it; clients can show a hint from this list.
/// </summary>
public sealed record AlbumSearchResponse(
    IReadOnlyList<AlbumSearchResultResponse> Albums,
    IReadOnlyList<string> UnavailableProviders)
{
    public static AlbumSearchResponse From(CatalogSearchResult result) => new(
        result.Albums.Select(AlbumSearchResultResponse.From).ToList(),
        result.FailedProviders);
}

/// <summary>
/// A search hit. The same fields are sent back when adding the album to a library, so the
/// service never has to look the album up at the provider a second time.
/// </summary>
public sealed record AlbumSearchResultResponse(
    string Provider,
    string ProviderAlbumId,
    string ArtistName,
    string AlbumName,
    string? CoverUrl,
    string AlbumUrl)
{
    public static AlbumSearchResultResponse From(CatalogAlbum album) => new(
        album.Provider,
        album.ProviderAlbumId,
        album.ArtistName,
        album.AlbumName,
        album.CoverUrl,
        album.AlbumUrl);
}
