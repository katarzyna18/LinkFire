using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace Linkfire.MusicLibrary.UnitTests.Application;

[TestFixture]
public class CompositeMusicCatalogProviderTests
{
    private static readonly CatalogAlbum DeezerAlbum = new("deezer", "1", "Daft Punk", "Discovery", null, "https://deezer.example/1");
    private static readonly CatalogAlbum SpotifyAlbum = new("spotify", "abc", "Daft Punk", "Discovery", null, "https://spotify.example/abc");

    [Test]
    public async Task Merges_results_from_every_provider()
    {
        var composite = Composite(
            new StubProvider("deezer", CatalogSearchResult.Success("deezer", [DeezerAlbum])),
            new StubProvider("spotify", CatalogSearchResult.Success("spotify", [SpotifyAlbum])));

        var result = await composite.SearchAlbumsAsync(Query(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Albums, Is.EqualTo(new[] { DeezerAlbum, SpotifyAlbum }));
            Assert.That(result.SucceededProviders, Is.EquivalentTo(new[] { "deezer", "spotify" }));
            Assert.That(result.FailedProviders, Is.Empty);
            Assert.That(result.IsUnavailable, Is.False);
        });
    }

    [Test]
    public async Task A_failing_provider_degrades_the_search_instead_of_failing_it()
    {
        var composite = Composite(
            new StubProvider("deezer", CatalogSearchResult.Success("deezer", [DeezerAlbum])),
            new StubProvider("spotify", CatalogSearchResult.Failed("spotify")));

        var result = await composite.SearchAlbumsAsync(Query(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Albums, Is.EqualTo(new[] { DeezerAlbum }));
            Assert.That(result.FailedProviders, Is.EqualTo(new[] { "spotify" }));
            Assert.That(result.IsUnavailable, Is.False);
        });
    }

    [Test]
    public async Task Search_is_unavailable_only_when_every_provider_failed()
    {
        var composite = Composite(
            new StubProvider("deezer", CatalogSearchResult.Failed("deezer")),
            new StubProvider("spotify", CatalogSearchResult.Failed("spotify")));

        var result = await composite.SearchAlbumsAsync(Query(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsUnavailable, Is.True);
            Assert.That(result.FailedProviders, Is.EquivalentTo(new[] { "deezer", "spotify" }));
        });
    }

    [Test]
    public async Task A_provider_that_throws_is_isolated_and_reported_as_failed()
    {
        var composite = Composite(
            new StubProvider("deezer", CatalogSearchResult.Success("deezer", [DeezerAlbum])),
            new StubProvider("broken", exception: new InvalidOperationException("bug")));

        var result = await composite.SearchAlbumsAsync(Query(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Albums, Is.EqualTo(new[] { DeezerAlbum }));
            Assert.That(result.FailedProviders, Is.EqualTo(new[] { "broken" }));
        });
    }

    [Test]
    public void Caller_cancellation_is_not_swallowed()
    {
        var composite = Composite(new StubProvider("deezer", exception: new OperationCanceledException()));

        Assert.That(
            () => composite.SearchAlbumsAsync(Query(), CancellationToken.None),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Requires_at_least_one_provider()
    {
        Assert.That(() => Composite(), Throws.ArgumentException);
    }

    private static CompositeMusicCatalogProvider Composite(params IMusicCatalogProvider[] providers) =>
        new(providers, NullLogger<CompositeMusicCatalogProvider>.Instance);

    private static AlbumSearchQuery Query()
    {
        Assert.That(AlbumSearchQuery.TryCreate("Discovery", "Daft Punk", out var query), Is.True);
        return query;
    }

    private sealed class StubProvider : IMusicCatalogProvider
    {
        private readonly CatalogSearchResult? _result;
        private readonly Exception? _exception;

        public StubProvider(string name, CatalogSearchResult? result = null, Exception? exception = null)
        {
            ProviderName = name;
            _result = result;
            _exception = exception;
        }

        public string ProviderName { get; }

        public Task<CatalogSearchResult> SearchAlbumsAsync(AlbumSearchQuery query, CancellationToken cancellationToken) =>
            _exception is null ? Task.FromResult(_result!) : Task.FromException<CatalogSearchResult>(_exception);
    }
}
