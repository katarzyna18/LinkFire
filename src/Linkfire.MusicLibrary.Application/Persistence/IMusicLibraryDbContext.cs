using Linkfire.MusicLibrary.Domain;
using Microsoft.EntityFrameworkCore;

namespace Linkfire.MusicLibrary.Application.Persistence;

/// <summary>
/// The application works against EF Core directly; this interface only hides the concrete
/// context (and therefore the database provider and migrations) from the application layer.
/// </summary>
public interface IMusicLibraryDbContext
{
    DbSet<User> Users { get; }

    DbSet<Library> Libraries { get; }

    DbSet<SavedAlbum> SavedAlbums { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
