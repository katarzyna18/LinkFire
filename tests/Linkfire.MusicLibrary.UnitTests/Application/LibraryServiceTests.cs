using Linkfire.MusicLibrary.Application;
using Linkfire.MusicLibrary.Application.Libraries;
using Linkfire.MusicLibrary.Application.Users;
using Linkfire.MusicLibrary.Domain;

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

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Name, Is.EqualTo("Kasia"));
            Assert.That(reloaded.Library.UserId, Is.EqualTo(userId));
        });
    }

    [Test]
    public async Task Adding_albums_persists_them_in_the_users_library()
    {
        var userId = await CreateUserAsync();

        AddAlbumsResult result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new LibraryService(context).AddAlbumsAsync(userId, [Discovery, Homework], CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        var library = await new LibraryService(verify).GetLibraryAsync(userId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Added, Has.Count.EqualTo(2));
            Assert.That(result.Skipped, Is.Empty);
            Assert.That(library.Albums.Select(a => a.ProviderAlbumId), Is.EquivalentTo(new[] { "302127", "302128" }));
        });
    }

    [Test]
    public async Task Adding_an_album_that_is_already_saved_is_skipped_not_duplicated()
    {
        var userId = await CreateUserAsync();
        await using (var context = _fixture.CreateContext())
        {
            await new LibraryService(context).AddAlbumsAsync(userId, [Discovery], CancellationToken.None);
        }

        AddAlbumsResult secondAttempt;
        await using (var context = _fixture.CreateContext())
        {
            secondAttempt = await new LibraryService(context).AddAlbumsAsync(userId, [Discovery, Homework], CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        var library = await new LibraryService(verify).GetLibraryAsync(userId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(secondAttempt.Added.Select(a => a.ProviderAlbumId), Is.EqualTo(new[] { "302128" }));
            Assert.That(secondAttempt.Skipped, Is.EqualTo(new[] { Discovery }));
            Assert.That(library.Albums, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Adding_the_same_album_twice_in_one_request_saves_it_once()
    {
        var userId = await CreateUserAsync();

        await using var context = _fixture.CreateContext();
        var result = await new LibraryService(context).AddAlbumsAsync(userId, [Discovery, Discovery], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Added, Has.Count.EqualTo(1));
            Assert.That(result.Skipped, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Removing_an_album_deletes_it_from_the_library()
    {
        var userId = await CreateUserAsync();
        Guid savedAlbumId;
        await using (var context = _fixture.CreateContext())
        {
            var result = await new LibraryService(context).AddAlbumsAsync(userId, [Discovery, Homework], CancellationToken.None);
            savedAlbumId = result.Added[0].Id;
        }

        await using (var context = _fixture.CreateContext())
        {
            await new LibraryService(context).RemoveAlbumAsync(userId, savedAlbumId, CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        var library = await new LibraryService(verify).GetLibraryAsync(userId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(library.Albums, Has.Count.EqualTo(1));
            Assert.That(library.Albums.Single().ProviderAlbumId, Is.EqualTo("302128"));
        });
    }

    [Test]
    public async Task Removing_an_unknown_album_reports_not_found()
    {
        var userId = await CreateUserAsync();

        await using var context = _fixture.CreateContext();
        var service = new LibraryService(context);

        Assert.That(
            () => service.RemoveAlbumAsync(userId, Guid.NewGuid(), CancellationToken.None),
            Throws.TypeOf<NotFoundException>());
    }

    [Test]
    public async Task Operations_on_an_unknown_user_report_not_found()
    {
        await using var context = _fixture.CreateContext();
        var libraryService = new LibraryService(context);
        var userService = new UserService(context);
        var unknownUser = Guid.NewGuid();

        Assert.Multiple(() =>
        {
            Assert.That(() => libraryService.GetLibraryAsync(unknownUser, CancellationToken.None), Throws.TypeOf<NotFoundException>());
            Assert.That(() => libraryService.AddAlbumsAsync(unknownUser, [Discovery], CancellationToken.None), Throws.TypeOf<NotFoundException>());
            Assert.That(() => userService.GetAsync(unknownUser, CancellationToken.None), Throws.TypeOf<NotFoundException>());
        });
    }

    private async Task<Guid> CreateUserAsync()
    {
        await using var context = _fixture.CreateContext();
        var user = await new UserService(context).CreateAsync("Kasia", CancellationToken.None);
        return user.Id;
    }
}
