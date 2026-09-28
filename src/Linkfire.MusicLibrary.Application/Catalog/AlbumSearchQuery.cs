namespace Linkfire.MusicLibrary.Application.Catalog;

public sealed record AlbumSearchQuery
{
    private AlbumSearchQuery(string? album, string? artist)
    {
        Album = album;
        Artist = artist;
    }

    public string? Album { get; }

    public string? Artist { get; }

    /// <summary>A query needs at least one criterion; blank input is treated as absent.</summary>
    public static bool TryCreate(string? album, string? artist, out AlbumSearchQuery query)
    {
        var normalizedAlbum = Normalize(album);
        var normalizedArtist = Normalize(artist);

        if (normalizedAlbum is null && normalizedArtist is null)
        {
            query = null!;
            return false;
        }

        query = new AlbumSearchQuery(normalizedAlbum, normalizedArtist);
        return true;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
