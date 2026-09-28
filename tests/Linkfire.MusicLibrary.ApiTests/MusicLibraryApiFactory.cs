using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Linkfire.MusicLibrary.ApiTests;

/// <summary>
/// Boots the real API with two substitutions: an in-memory SQLite database (so every test run starts
/// empty and nothing is written to disk) and a fake catalogue provider (so tests never call Deezer).
/// </summary>
public sealed class MusicLibraryApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public FakeMusicCatalogProvider Catalog { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            _connection.Open();

            services.RemoveAll<DbContextOptions<MusicLibraryDbContext>>();
            services.AddDbContext<MusicLibraryDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IMusicCatalogProvider>();
            services.AddSingleton<IMusicCatalogProvider>(Catalog);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
