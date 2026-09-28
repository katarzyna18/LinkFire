namespace Linkfire.MusicLibrary.Domain;

/// <summary>
/// Provider-neutral description of an album as it comes from a catalogue search result.
/// This is the only shape the library needs in order to remember an album.
/// </summary>
public sealed record CatalogAlbum(
    string Provider,
    string ProviderAlbumId,
    string ArtistName,
    string AlbumName,
    string? CoverUrl,
    string AlbumUrl);
