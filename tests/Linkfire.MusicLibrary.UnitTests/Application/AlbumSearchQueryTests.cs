using Linkfire.MusicLibrary.Application.Catalog;

namespace Linkfire.MusicLibrary.UnitTests.Application;

[TestFixture]
public class AlbumSearchQueryTests
{
    [TestCase(null, null)]
    [TestCase("", "")]
    [TestCase("   ", null)]
    public void TryCreate_rejects_queries_without_any_criterion(string? album, string? artist)
    {
        var created = AlbumSearchQuery.TryCreate(album, artist, out _);

        Assert.That(created, Is.False);
    }

    [Test]
    public void TryCreate_trims_values_and_treats_blank_values_as_absent()
    {
        var created = AlbumSearchQuery.TryCreate("  Discovery ", "   ", out var query);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(query.Album, Is.EqualTo("Discovery"));
            Assert.That(query.Artist, Is.Null);
        });
    }
}
