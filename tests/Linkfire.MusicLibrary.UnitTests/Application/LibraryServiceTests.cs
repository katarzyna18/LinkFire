using Linkfire.MusicLibrary.Application;
using Linkfire.MusicLibrary.Application.Libraries;
using Linkfire.MusicLibrary.Application.Users;
using Linkfire.MusicLibrary.Domain;
using Linkfire.MusicLibrary.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Linkfire.MusicLibrary.UnitTests.Application;

[TestFixture]
public class LibraryServiceTests
{
    private static readonly CatalogAlbum Discovery = new(
        "deezer", "302127", "Daft Punk", "Discovery", "https://cdn.example/discovery.jpg", "https://www.deezer.com/album/302127");

    private static readonly CatalogAlbum Homework = new(
        "deezer", "302128", "Daft Punk", "Homework", null, "https://www.deezer.com/album/302128");

    private SqliteDbContextFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new SqliteDbContextFixture();

    [TearDown]
    public void TearDown() => _fixture.Dispose();

    [Test]
    public async Task Creating_a_user_persists_the_user_with_one_library()
    {
        Guid userId;
        await using (var context = _fixture.CreateContext())
        {
            var user = await new UserService(context).CreateAsync("Kasia", CancellationToken.None);
            userId = user.Id;
        }

        await using var verify = _fixture.CreateContext();
        var reloaded = await new UserService(verify).GetAsync(userId, CancellationToken.None);

        Assert.That(reloaded.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Value.Name, Is.EqualTo("Kasia"));
            Assert.That(reloaded.Value.Library.UserId, Is.EqualTo(userId));
        });
    }

    [Test]
    public async Task Adding_albums_persists_them_in_the_users_library()
    {
        var userId = await CreateUserAsync();

        Result<AddAlbumsResult> result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Service(context).AddAlbumsAsync(userId, [Discovery, Homework], CancellationToken.None);
        }

        var page = await GetPageAsync(userId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Added, Has.Count.EqualTo(2));
            Assert.That(result.Value.Skipped, Is.Empty);
            Assert.That(page.Albums.Select(a => a.ProviderAlbumId), Is.EqualTo(new[] { "302127", "302128" }));
            Assert.That(page.TotalCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Adding_an_album_that_is_already_saved_is_skipped_not_duplicated()
    {
        var userId = await CreateUserAsync();
        await using (var context = _fixture.CreateContext())
        {
            await Service(context).AddAlbumsAsync(userId, [Discovery], CancellationToken.None);
        }

        Result<AddAlbumsResult> secondAttempt;
        await using (var context = _fixture.CreateContext())
        {
            secondAttempt = await Service(context).AddAlbumsAsync(userId, [Discovery, Homework], CancellationToken.None);
        }

        var page = await GetPageAsync(userId);

        Assert.That(secondAttempt.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(secondAttempt.Value.Added.Select(a => a.ProviderAlbumId), Is.EqualTo(new[] { "302128" }));
            Assert.That(secondAttempt.Value.Skipped, Is.EqualTo(new[] { Discovery }));
            Assert.That(page.TotalCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Adding_the_same_album_twice_in_one_request_saves_it_once()
    {
        var userId = await CreateUserAsync();

        await using var context = _fixture.CreateContext();
        var result = await Service(context).AddAlbumsAsync(userId, [Discovery, Discovery], CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Added, Has.Count.EqualTo(1));
            Assert.That(result.Value.Skipped, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Concurrent_add_of_the_same_album_is_reported_as_a_conflict()
    {
        var userId = await CreateUserAsync();

        await using var context = _fixture.CreateContext();
        // Simulate another request winning the race: it inserts the same album after this
        // context has loaded the (still empty) library but before its INSERT runs.
        context.SavingChanges += (_, _) =>
        {
            using var other = _fixture.CreateContext();
            Service(other).AddAlbumsAsync(userId, [Discovery], CancellationToken.None).GetAwaiter().GetResult();
        };

        var result = await Service(context).AddAlbumsAsync(userId, [Discovery], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.Conflict));
        });
    }

    [Test]
    public async Task Library_pages_are_ordered_by_addition_and_report_the_total()
    {
        var userId = await CreateUserAsync();
        await using (var context = _fixture.CreateContext())
        {
            var service = Service(context);
            for (var i = 1; i <= 5; i++)
            {
                await service.AddAlbumsAsync(userId, [Discovery with { ProviderAlbumId = i.ToString() }], CancellationToken.None);
            }
        }

        var firstPage = await GetPageAsync(userId, new PageRequest(page: 1, pageSize: 2));
        var lastPage = await GetPageAsync(userId, new PageRequest(page: 3, pageSize: 2));

        Assert.Multiple(() =>
        {
            Assert.That(firstPage.Albums.Select(a => a.ProviderAlbumId), Is.EqualTo(new[] { "1", "2" }));
            Assert.That(firstPage.TotalCount, Is.EqualTo(5));
            Assert.That(lastPage.Albums.Select(a => a.ProviderAlbumId), Is.EqualTo(new[] { "5" }));
        });
    }

    [Test]
    public async Task Removing_an_album_deletes_it_from_the_library()
    {
        var userId = await CreateUserAsync();
        Guid savedAlbumId;
        await using (var context = _fixture.CreateContext())
        {
            var result = await Service(context).AddAlbumsAsync(userId, [Discovery, Homework], CancellationToken.None);
            savedAlbumId = result.Value.Added[0].Id;
        }

        Result removal;
        await using (var context = _fixture.CreateContext())
        {
            removal = await Service(context).RemoveAlbumAsync(userId, savedAlbumId, CancellationToken.None);
        }

        var page = await GetPageAsync(userId);

        Assert.Multiple(() =>
        {
            Assert.That(removal.IsSuccess, Is.True);
            Assert.That(page.TotalCount, Is.EqualTo(1));
            Assert.That(page.Albums.Single().ProviderAlbumId, Is.EqualTo("302128"));
        });
    }

    [Test]
    public async Task Removing_an_unknown_album_reports_not_found()
    {
        var userId = await CreateUserAsync();

        await using var context = _fixture.CreateContext();
        var result = await Service(context).RemoveAlbumAsync(userId, Guid.NewGuid(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error!.Kind, Is.EqualTo(ErrorKind.NotFound));
        });
    }

    [Test]
    public async Task Operations_on_an_unknown_user_report_not_found()
    {
        await using var context = _fixture.CreateContext();
        var libraryService = Service(context);
        var unknownUser = Guid.NewGuid();

        var page = await libraryService.GetLibraryPageAsync(unknownUser, new PageRequest(), CancellationToken.None);
        var add = await libraryService.AddAlbumsAsync(unknownUser, [Discovery], CancellationToken.None);
        var user = await new UserService(context).GetAsync(unknownUser, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(page.Error?.Kind, Is.EqualTo(ErrorKind.NotFound));
            Assert.That(add.Error?.Kind, Is.EqualTo(ErrorKind.NotFound));
            Assert.That(user.Error?.Kind, Is.EqualTo(ErrorKind.NotFound));
        });
    }

    private static LibraryService Service(MusicLibraryDbContext context) =>
        new(context, NullLogger<LibraryService>.Instance);

    private async Task<Guid> CreateUserAsync()
    {
        await using var context = _fixture.CreateContext();
        var user = await new UserService(context).CreateAsync("Kasia", CancellationToken.None);
        return user.Id;
    }

    private async Task<LibraryPage> GetPageAsync(Guid userId, PageRequest? page = null)
    {
        await using var context = _fixture.CreateContext();
        var result = await Service(context).GetLibraryPageAsync(userId, page ?? new PageRequest(), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
        return result.Value;
    }
}
