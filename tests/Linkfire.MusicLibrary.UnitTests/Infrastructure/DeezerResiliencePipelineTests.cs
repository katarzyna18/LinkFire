using System.Net;
using System.Text;
using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

namespace Linkfire.MusicLibrary.UnitTests.Infrastructure;

/// <summary>
/// Exercises the Deezer client exactly as the application registers it (typed client + resilience
/// pipeline), with the network replaced by a scripted handler.
/// </summary>
[TestFixture]
public class DeezerResiliencePipelineTests
{
    private const string OnePayload = """{ "data": [ { "id": 1, "title": "Discovery", "link": "https://www.deezer.com/album/1", "artist": { "name": "Daft Punk" } } ] }""";

    [Test]
    public async Task Transient_server_errors_are_retried_and_the_search_still_succeeds()
    {
        var handler = new ScriptedHandler(
            () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            () => Json(OnePayload));
        var provider = ResolveDeezer(handler, maxRetries: 2);

        var result = await provider.SearchAlbumsAsync(Query(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Calls, Is.EqualTo(3));
            Assert.That(result.IsUnavailable, Is.False);
            Assert.That(result.Albums, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Retries_stop_at_the_configured_limit_and_the_provider_is_reported_as_failed()
    {
        var handler = new ScriptedHandler(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var provider = ResolveDeezer(handler, maxRetries: 1);

        var result = await provider.SearchAlbumsAsync(Query(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Calls, Is.EqualTo(2), "one attempt plus one retry");
            Assert.That(result.IsUnavailable, Is.True);
        });
    }

    [Test]
    public async Task Client_errors_are_not_retried()
    {
        var handler = new ScriptedHandler(() => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var provider = ResolveDeezer(handler, maxRetries: 2);

        var result = await provider.SearchAlbumsAsync(Query(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Calls, Is.EqualTo(1));
            Assert.That(result.IsUnavailable, Is.True);
        });
    }

    private static IMusicCatalogProvider ResolveDeezer(ScriptedHandler handler, int maxRetries)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Deezer:BaseUrl"] = "https://deezer.test/",
                ["Deezer:AttemptTimeoutSeconds"] = "2",
                ["Deezer:TotalTimeoutSeconds"] = "10",
                ["Deezer:MaxRetries"] = maxRetries.ToString(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddDeezerCatalogProvider(configuration);
        services.ConfigureHttpClientDefaults(http => http.ConfigurePrimaryHttpMessageHandler(() => handler));
        // Retries should not slow the test suite down.
        services.PostConfigureAll<HttpStandardResilienceOptions>(o => o.Retry.Delay = TimeSpan.Zero);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredKeyedService<IMusicCatalogProvider>(MusicCatalogProviders.LeafServiceKey);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private static AlbumSearchQuery Query()
    {
        Assert.That(AlbumSearchQuery.TryCreate("Discovery", "Daft Punk", out var query), Is.True);
        return query;
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage>[] _responses;

        public ScriptedHandler(params Func<HttpResponseMessage>[] responses)
        {
            _responses = responses;
        }

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var next = _responses[Math.Min(Calls, _responses.Length - 1)];
            Calls++;
            return Task.FromResult(next());
        }
    }
}
