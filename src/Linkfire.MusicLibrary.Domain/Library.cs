namespace Linkfire.MusicLibrary.Domain;

public class Library
{
    private readonly List<SavedAlbum> _albums = [];

    private Library()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>
    /// The albums currently loaded for this library. Callers load only what an operation needs
    /// (for example the albums that could collide with an add), so this is not necessarily the whole library.
    /// </summary>
    public IReadOnlyCollection<SavedAlbum> Albums => _albums;

    internal static Library CreateFor(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
    };

    /// <summary>
    /// Adds an album unless the same provider album is already in the library.
    /// Returns the saved album when added, or <c>null</c> when it was a duplicate.
    /// </summary>
    public SavedAlbum? AddAlbum(CatalogAlbum album)
    {
        if (Contains(album.Provider, album.ProviderAlbumId))
        {
            return null;
        }

        var saved = SavedAlbum.Create(Id, album);
        _albums.Add(saved);
        return saved;
    }

    public bool RemoveAlbum(Guid savedAlbumId)
    {
        var album = _albums.FirstOrDefault(a => a.Id == savedAlbumId);
        if (album is null)
        {
            return false;
        }

        _albums.Remove(album);
        return true;
    }

    public bool Contains(string provider, string providerAlbumId) =>
        _albums.Any(a => a.Matches(provider, providerAlbumId));
}
