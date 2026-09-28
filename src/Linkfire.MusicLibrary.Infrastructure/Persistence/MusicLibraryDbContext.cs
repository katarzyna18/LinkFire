using Linkfire.MusicLibrary.Application.Persistence;
using Linkfire.MusicLibrary.Domain;
using Microsoft.EntityFrameworkCore;

namespace Linkfire.MusicLibrary.Infrastructure.Persistence;

public sealed class MusicLibraryDbContext : DbContext, IMusicLibraryDbContext
{
    public MusicLibraryDbContext(DbContextOptions<MusicLibraryDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Library> Libraries => Set<Library>();

    public DbSet<SavedAlbum> SavedAlbums => Set<SavedAlbum>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identifiers are assigned by the domain, not the database. Declaring that also makes EF treat
        // albums reached through Library.Albums as new rows instead of assuming they already exist.
        modelBuilder.Entity<User>().Property(u => u.Id).ValueGeneratedNever();
        modelBuilder.Entity<Library>().Property(l => l.Id).ValueGeneratedNever();
        modelBuilder.Entity<SavedAlbum>().Property(a => a.Id).ValueGeneratedNever();

        modelBuilder.Entity<User>(user =>
        {
            user.HasKey(u => u.Id);
            user.Property(u => u.Name).IsRequired().HasMaxLength(200);

            // Exactly one library per user: the FK lives on Library and is unique.
            user.HasOne(u => u.Library)
                .WithOne()
                .HasForeignKey<Library>(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Library>(library =>
        {
            library.HasKey(l => l.Id);
            library.HasIndex(l => l.UserId).IsUnique();

            library.HasMany(l => l.Albums)
                .WithOne()
                .HasForeignKey(a => a.LibraryId)
                .OnDelete(DeleteBehavior.Cascade);

            library.Navigation(l => l.Albums).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<SavedAlbum>(album =>
        {
            album.HasKey(a => a.Id);
            album.Property(a => a.Provider).IsRequired().HasMaxLength(50);
            album.Property(a => a.ProviderAlbumId).IsRequired().HasMaxLength(200);
            album.Property(a => a.ArtistName).IsRequired().HasMaxLength(500);
            album.Property(a => a.AlbumName).IsRequired().HasMaxLength(500);
            album.Property(a => a.CoverUrl).HasMaxLength(2000);
            album.Property(a => a.AlbumUrl).IsRequired().HasMaxLength(2000);

            // Database-level guarantee behind Library.AddAlbum's duplicate check.
            album.HasIndex(a => new { a.LibraryId, a.Provider, a.ProviderAlbumId }).IsUnique();
        });
    }
}
