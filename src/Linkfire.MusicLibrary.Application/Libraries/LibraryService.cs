using Linkfire.MusicLibrary.Application.Persistence;
using Linkfire.MusicLibrary.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Linkfire.MusicLibrary.Application.Libraries;

public sealed class LibraryService
{
    private readonly IMusicLibraryDbContext _dbContext;
    private readonly ILogger<LibraryService> _logger;

    public LibraryService(IMusicLibraryDbContext dbContext, ILogger<LibraryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>Returns one page of the user's library, oldest additions first.</summary>
    public async Task<Result<LibraryPage>> GetLibraryPageAsync(
        Guid userId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var library = await _dbContext.Libraries
            .AsNoTracking()
            .SingleOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        if (library is null)
        {
            return Error.UserNotFound(userId);
        }

        var albumsQuery = _dbContext.SavedAlbums
            .AsNoTracking()
            .Where(a => a.LibraryId == library.Id);

        var totalCount = await albumsQuery.CountAsync(cancellationToken);
        var albums = await albumsQuery
            .OrderBy(a => a.AddedAt)
            .ThenBy(a => a.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new LibraryPage(library.Id, library.UserId, albums, page.Page, page.PageSize, totalCount);
    }

    /// <summary>
    /// Adds albums to the user's library. Adding is idempotent: albums that are already present
    /// (or repeated within the same request) are reported as skipped rather than rejected.
    /// </summary>
    public async Task<Result<AddAlbumsResult>> AddAlbumsAsync(
        Guid userId,
        IReadOnlyCollection<CatalogAlbum> albums,
        CancellationToken cancellationToken)
    {
        // Only the albums that could collide with this request are loaded; the library may hold thousands.
        var candidateIds = albums.Select(a => a.ProviderAlbumId).Distinct().ToList();

        var library = await _dbContext.Libraries
            .Include(l => l.Albums.Where(a => candidateIds.Contains(a.ProviderAlbumId)))
            .SingleOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        if (library is null)
        {
            return Error.UserNotFound(userId);
        }

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

        if (added.Count == 0)
        {
            return new AddAlbumsResult(added, skipped);
        }

        var save = await SaveAsync(cancellationToken);
        return save.IsSuccess ? new AddAlbumsResult(added, skipped) : save.Error;
    }

    public async Task<Result> RemoveAlbumAsync(Guid userId, Guid savedAlbumId, CancellationToken cancellationToken)
    {
        var library = await _dbContext.Libraries
            .Include(l => l.Albums.Where(a => a.Id == savedAlbumId))
            .SingleOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        if (library is null)
        {
            return Error.UserNotFound(userId);
        }

        if (!library.RemoveAlbum(savedAlbumId))
        {
            return Error.SavedAlbumNotFound(savedAlbumId);
        }

        return await SaveAsync(cancellationToken);
    }

    // The in-memory duplicate check cannot see a concurrent request adding the same album; the unique
    // index catches that and it is reported as a retryable conflict instead of an unhandled error.
    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Library update rejected by the database; reporting as conflict");
            return Error.Conflict("The library was modified by another request. Please retry.");
        }
    }
}
