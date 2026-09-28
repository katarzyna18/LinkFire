namespace Linkfire.MusicLibrary.Domain;

public class SavedAlbum
{
    private SavedAlbum()
    {
    }

    public Guid Id { get; private set; }

    public Guid LibraryId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderAlbumId { get; private set; } = string.Empty;

    public string ArtistName { get; private set; } = string.Empty;

    public string AlbumName { get; private set; } = string.Empty;

    public string? CoverUrl { get; private set; }

    public string AlbumUrl { get; private set; } = string.Empty;

    // UTC. Kept as DateTime rather than DateTimeOffset so every relational provider can sort on it.
    public DateTime AddedAt { get; private set; }

    internal static SavedAlbum Create(Guid libraryId, CatalogAlbum album) => new()
    {
        Id = Guid.NewGuid(),
        LibraryId = libraryId,
        // Stored lower-case so the database unique index agrees with the case-insensitive Matches() rule.
        Provider = album.Provider.Trim().ToLowerInvariant(),
        ProviderAlbumId = album.ProviderAlbumId,
        ArtistName = album.ArtistName,
        AlbumName = album.AlbumName,
        CoverUrl = album.CoverUrl,
        AlbumUrl = album.AlbumUrl,
        AddedAt = DateTime.UtcNow,
    };

    // Provider names are treated case-insensitively ("Deezer" and "deezer" are the same catalogue),
    // while provider album IDs are compared exactly because providers define their own ID formats.
    public bool Matches(string provider, string providerAlbumId) =>
        string.Equals(Provider, provider, StringComparison.OrdinalIgnoreCase)
        && string.Equals(ProviderAlbumId, providerAlbumId, StringComparison.Ordinal);
}
