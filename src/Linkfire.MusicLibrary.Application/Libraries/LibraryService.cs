using Linkfire.MusicLibrary.Application.Persistence;
using Linkfire.MusicLibrary.Domain;
using Microsoft.EntityFrameworkCore;

namespace Linkfire.MusicLibrary.Application.Libraries;

public sealed class LibraryService
{
    private readonly IMusicLibraryDbContext _dbContext;

    public LibraryService(IMusicLibraryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Library> GetLibraryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var library = await _dbContext.Libraries
            .Include(l => l.Albums)
            .SingleOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        return library ?? throw NotFoundException.User(userId);
    }

    /// <summary>
    /// Adds albums to the user's library. Adding is idempotent: albums that are already present
    /// (or repeated within the same request) are reported as skipped rather than rejected.
    /// </summary>
    public async Task<AddAlbumsResult> AddAlbumsAsync(
        Guid userId,
        IReadOnlyCollection<CatalogAlbum> albums,
        CancellationToken cancellationToken)
    {
        var library = await GetLibraryAsync(userId, cancellationToken);

        var added = new List<SavedAlbum>();
        var skipped = new List<CatalogAlbum>();

        foreach (var album in albums)
        {
            var saved = library.AddAlbum(album);
            if (saved is null)
            {
                skipped.Add(album);
            }
            else
            {
                added.Add(saved);
            }
        }

        if (added.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AddAlbumsResult(added, skipped);
    }

    public async Task RemoveAlbumAsync(Guid userId, Guid savedAlbumId, CancellationToken cancellationToken)
    {
        var library = await GetLibraryAsync(userId, cancellationToken);

        if (!library.RemoveAlbum(savedAlbumId))
        {
            throw NotFoundException.SavedAlbum(savedAlbumId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
