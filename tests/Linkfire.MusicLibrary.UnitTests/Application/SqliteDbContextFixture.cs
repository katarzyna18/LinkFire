using Linkfire.MusicLibrary.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Linkfire.MusicLibrary.UnitTests.Application;

/// <summary>
/// Real EF Core against an in-memory SQLite database, so service tests exercise the actual mapping,
/// indexes and change tracking instead of an in-memory provider that behaves differently.
/// The connection must stay open for the lifetime of the database.
/// </summary>
public sealed class SqliteDbContextFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MusicLibraryDbContext> _options;

    public SqliteDbContextFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MusicLibraryDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    // A fresh context per call mirrors the per-request scope used by the API.
    public MusicLibraryDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
