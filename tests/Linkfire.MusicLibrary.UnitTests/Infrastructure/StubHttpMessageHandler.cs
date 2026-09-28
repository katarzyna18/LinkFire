using System.Net;
using System.Text;

namespace Linkfire.MusicLibrary.UnitTests.Infrastructure;

/// <summary>
/// Lets a test decide how "Deezer" answers, and records the request so the outgoing URL can be asserted.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    public static StubHttpMessageHandler RespondingWith(HttpStatusCode statusCode, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    public static StubHttpMessageHandler NeverResponding() =>
        new(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return _respond(request, cancellationToken);
    }
}
