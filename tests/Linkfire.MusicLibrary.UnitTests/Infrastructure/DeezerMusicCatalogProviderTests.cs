using System.Net;
using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Infrastructure.Deezer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Linkfire.MusicLibrary.UnitTests.Infrastructure;

[TestFixture]
public class DeezerMusicCatalogProviderTests
{
    private const string TwoAlbumsPayload = """
        {
          "data": [
            {
              "id": 302127,
              "title": "Discovery",
              "link": "https://www.deezer.com/album/302127",
              "cover": "https://cdn.example/302127/cover.jpg",
              "cover_medium": "https://cdn.example/302127/250x250.jpg",
              "artist": { "id": 27, "name": "Daft Punk" },
              "type": "album"
            },
            {
              "id": 302128,
              "title": "Homework",
              "link": "https://www.deezer.com/album/302128",
              "cover": "https://cdn.example/302128/cover.jpg",
              "artist": { "id": 27, "name": "Daft Punk" },
              "type": "album"
            }
          ],
          "total": 2
        }
        """;

    [Test]
    public async Task Maps_deezer_albums_into_provider_neutral_results()
    {
        var provider = CreateProvider(StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, TwoAlbumsPayload));

        var result = await provider.SearchAlbumsAsync(Query("Discovery", "Daft Punk"), CancellationToken.None);

        Assert.That(result.SucceededProviders, Is.EqualTo(new[] { "deezer" }));
        Assert.That(result.Albums, Has.Count.EqualTo(2));
        var first = result.Albums[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.Provider, Is.EqualTo("deezer"));
            Assert.That(first.ProviderAlbumId, Is.EqualTo("302127"));
            Assert.That(first.ArtistName, Is.EqualTo("Daft Punk"));
            Assert.That(first.AlbumName, Is.EqualTo("Discovery"));
            Assert.That(first.CoverUrl, Is.EqualTo("https://cdn.example/302127/250x250.jpg"), "prefers the medium cover");
            Assert.That(first.AlbumUrl, Is.EqualTo("https://www.deezer.com/album/302127"));
            Assert.That(result.Albums[1].CoverUrl, Is.EqualTo("https://cdn.example/302128/cover.jpg"), "falls back to the default cover");
        });
    }

    [Test]
    public async Task Sends_both_search_terms_and_the_configured_limit_to_the_album_search_endpoint()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{ "data": [], "total": 0 }""");
        var provider = CreateProvider(handler, maxResults: 10);

        await provider.SearchAlbumsAsync(Query("Discovery", "Daft Punk"), CancellationToken.None);

        var uri = handler.LastRequest!.RequestUri!;
        Assert.Multiple(() =>
        {
            Assert.That(uri.AbsolutePath, Is.EqualTo("/search/album"));
            Assert.That(uri.Query, Does.Contain("q=Daft%20Punk%20Discovery"));
            Assert.That(uri.Query, Does.Contain("limit=10"));
        });
    }

    [Test]
    public async Task Returns_a_successful_empty_result_when_deezer_has_no_matches()
    {
        var provider = CreateProvider(StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{ "data": [], "total": 0 }"""));

        var result = await provider.SearchAlbumsAsync(Query("zzzz-no-such-album", null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Albums, Is.Empty);
            Assert.That(result.IsUnavailable, Is.False);
        });
    }

    [Test]
    public async Task Skips_entries_that_cannot_be_identified_or_linked()
    {
        const string payload = """
            {
              "data": [
                { "id": 0, "title": "No id", "link": "https://www.deezer.com/album/0", "artist": { "name": "X" } },
                { "id": 5, "title": "No link", "artist": { "name": "X" } },
                { "id": 7, "title": "Valid", "link": "https://www.deezer.com/album/7" }
              ]
            }
            """;
        var provider = CreateProvider(StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, payload));

        var result = await provider.SearchAlbumsAsync(Query("Valid", null), CancellationToken.None);

        Assert.That(result.Albums.Select(r => r.ProviderAlbumId), Is.EqualTo(new[] { "7" }));
        Assert.That(result.Albums[0].ArtistName, Is.EqualTo("Unknown artist"));
    }

    [Test]
    public async Task Reports_the_provider_as_failed_on_a_non_success_status()
    {
        var provider = CreateProvider(StubHttpMessageHandler.RespondingWith(HttpStatusCode.InternalServerError, "boom"));

        var result = await provider.SearchAlbumsAsync(Query("Discovery", null), CancellationToken.None);

        AssertFailed(result);
    }

    [Test]
    public async Task Reports_the_provider_as_failed_on_a_network_failure()
    {
        var provider = CreateProvider(StubHttpMessageHandler.Throwing(new HttpRequestException("connection refused")));

        var result = await provider.SearchAlbumsAsync(Query("Discovery", null), CancellationToken.None);

        AssertFailed(result);
    }

    [Test]
    public async Task Reports_the_provider_as_failed_when_deezer_times_out()
    {
        var provider = CreateProvider(StubHttpMessageHandler.NeverResponding(), timeout: TimeSpan.FromMilliseconds(100));

        var result = await provider.SearchAlbumsAsync(Query("Discovery", null), CancellationToken.None);

        AssertFailed(result);
    }

    [Test]
    public void Propagates_caller_cancellation_instead_of_reporting_a_provider_failure()
    {
        var provider = CreateProvider(StubHttpMessageHandler.NeverResponding());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.That(
            () => provider.SearchAlbumsAsync(Query("Discovery", null), cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task Reports_the_provider_as_failed_on_a_malformed_response()
    {
        var provider = CreateProvider(StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, "<html>not json</html>"));

        var result = await provider.SearchAlbumsAsync(Query("Discovery", null), CancellationToken.None);

        AssertFailed(result);
    }

    [Test]
    public async Task Reports_the_provider_as_failed_when_deezer_returns_an_error_payload()
    {
        // Deezer signals quota problems with HTTP 200 and an "error" object.
        const string payload = """{ "error": { "type": "Exception", "message": "Quota limit exceeded", "code": 4 } }""";
        var provider = CreateProvider(StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK, payload));

        var result = await provider.SearchAlbumsAsync(Query("Discovery", null), CancellationToken.None);

        AssertFailed(result);
    }

    private static void AssertFailed(CatalogSearchResult result)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.IsUnavailable, Is.True);
            Assert.That(result.FailedProviders, Is.EqualTo(new[] { "deezer" }));
            Assert.That(result.Albums, Is.Empty);
        });
    }

    private static AlbumSearchQuery Query(string? album, string? artist)
    {
        Assert.That(AlbumSearchQuery.TryCreate(album, artist, out var query), Is.True);
        return query;
    }

    private static DeezerMusicCatalogProvider CreateProvider(
        StubHttpMessageHandler handler,
        int maxResults = 25,
        TimeSpan? timeout = null)
    {
        var options = new DeezerOptions { MaxResults = maxResults };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = timeout ?? TimeSpan.FromSeconds(options.AttemptTimeoutSeconds),
        };

        return new DeezerMusicCatalogProvider(
            httpClient,
            Options.Create(options),
            NullLogger<DeezerMusicCatalogProvider>.Instance);
    }
}
