using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.UnitTests.Domain;

[TestFixture]
public class LibraryTests
{
    private static readonly CatalogAlbum Discovery = new(
        Provider: "deezer",
        ProviderAlbumId: "302127",
        ArtistName: "Daft Punk",
        AlbumName: "Discovery",
        CoverUrl: "https://cdn.example/discovery.jpg",
        AlbumUrl: "https://www.deezer.com/album/302127");

    [Test]
    public void Create_user_creates_exactly_one_library_owned_by_that_user()
    {
        var user = User.Create("  Kasia ");

        Assert.Multiple(() =>
        {
            Assert.That(user.Name, Is.EqualTo("Kasia"));
            Assert.That(user.Library, Is.Not.Null);
            Assert.That(user.Library.UserId, Is.EqualTo(user.Id));
            Assert.That(user.Library.Albums, Is.Empty);
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Create_user_rejects_blank_name(string name)
    {
        Assert.That(() => User.Create(name), Throws.ArgumentException);
    }

    [Test]
    public void AddAlbum_stores_provider_neutral_album_data()
    {
        var library = User.Create("Kasia").Library;

        var saved = library.AddAlbum(Discovery);

        Assert.That(saved, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(saved!.LibraryId, Is.EqualTo(library.Id));
            Assert.That(saved.Provider, Is.EqualTo("deezer"));
            Assert.That(saved.ProviderAlbumId, Is.EqualTo("302127"));
            Assert.That(saved.ArtistName, Is.EqualTo("Daft Punk"));
            Assert.That(saved.AlbumName, Is.EqualTo("Discovery"));
            Assert.That(saved.CoverUrl, Is.EqualTo("https://cdn.example/discovery.jpg"));
            Assert.That(saved.AlbumUrl, Is.EqualTo("https://www.deezer.com/album/302127"));
            Assert.That(library.Albums, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AddAlbum_ignores_the_same_provider_album_added_twice()
    {
        var library = User.Create("Kasia").Library;
        library.AddAlbum(Discovery);

        var second = library.AddAlbum(Discovery with { AlbumName = "Discovery (Remastered)" });

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.Null);
            Assert.That(library.Albums, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AddAlbum_treats_provider_name_case_insensitively()
    {
        var library = User.Create("Kasia").Library;
        library.AddAlbum(Discovery);

        var second = library.AddAlbum(Discovery with { Provider = "Deezer" });

        Assert.That(second, Is.Null);
    }

    [Test]
    public void AddAlbum_allows_the_same_id_from_a_different_provider()
    {
        var library = User.Create("Kasia").Library;
        library.AddAlbum(Discovery);

        var fromSpotify = library.AddAlbum(Discovery with { Provider = "spotify" });

        Assert.Multiple(() =>
        {
            Assert.That(fromSpotify, Is.Not.Null);
            Assert.That(library.Albums, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void RemoveAlbum_removes_an_existing_album_and_reports_unknown_ids()
    {
        var library = User.Create("Kasia").Library;
        var saved = library.AddAlbum(Discovery)!;

        var removed = library.RemoveAlbum(saved.Id);
        var removedAgain = library.RemoveAlbum(saved.Id);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(removedAgain, Is.False);
            Assert.That(library.Albums, Is.Empty);
        });
    }
}
