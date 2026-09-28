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
            await SaveAsync(cancellationToken);
        }

        return new AddAlbumsResult(added, skipped);
    }

    // The in-memory duplicate check above cannot see a concurrent request adding the same album;
    // the unique index catches that and we report it as a retryable conflict instead of a 500.
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new ConflictException("The library was modified by another request. Please retry.", ex);
        }
    }

    public async Task RemoveAlbumAsync(Guid userId, Guid savedAlbumId, CancellationToken cancellationToken)
    {
        var library = await GetLibraryAsync(userId, cancellationToken);

        if (!library.RemoveAlbum(savedAlbumId))
        {
            throw NotFoundException.SavedAlbum(savedAlbumId);
        }

        await SaveAsync(cancellationToken);
    }
}
