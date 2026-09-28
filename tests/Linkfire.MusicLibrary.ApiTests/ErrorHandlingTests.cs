using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Linkfire.MusicLibrary.ApiTests;

[TestFixture]
public class ErrorHandlingTests : ApiTestBase
{
    [Test]
    public async Task Unknown_user_returns_404_problem_details()
    {
        var response = await Client.GetAsync($"/api/users/{Guid.NewGuid()}/library");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Multiple(() =>
        {
            Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo(404));
            Assert.That(problem.GetProperty("detail").GetString(), Does.Contain("was not found"));
        });
    }

    [Test]
    public async Task Adding_albums_to_an_unknown_user_returns_404()
    {
        var response = await Client.PostAsJsonAsync($"/api/users/{Guid.NewGuid()}/library/albums", AddAlbumsBody(Discovery));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task Creating_a_user_with_a_blank_name_returns_400(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/users", new { name });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.That(problem.GetProperty("errors").TryGetProperty("Name", out _), Is.True);
    }

    [Test]
    public async Task Adding_an_empty_album_collection_returns_400()
    {
        var userId = await CreateUserAsync();

        var response = await Client.PostAsJsonAsync($"/api/users/{userId}/library/albums", new { albums = Array.Empty<object>() });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Adding_an_album_without_provider_identifiers_returns_400()
    {
        var userId = await CreateUserAsync();
        var body = new
        {
            albums = new[]
            {
                new { artistName = "Daft Punk", albumName = "Discovery", albumUrl = "https://music.example/album/1" },
            },
        };

        var response = await Client.PostAsJsonAsync($"/api/users/{userId}/library/albums", body);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = problem.GetProperty("errors");
        Assert.Multiple(() =>
        {
            Assert.That(errors.TryGetProperty("Albums[0].Provider", out _), Is.True);
            Assert.That(errors.TryGetProperty("Albums[0].ProviderAlbumId", out _), Is.True);
        });
    }

    [Test]
    public async Task Malformed_json_returns_400()
    {
        var response = await Client.PostAsync("/api/users", new StringContent("{ \"name\": ", System.Text.Encoding.UTF8, "application/json"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Search_without_album_or_artist_returns_400()
    {
        var response = await Client.GetAsync("/api/albums/search");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Search_with_an_overlong_term_returns_400_without_calling_the_provider()
    {
        var response = await Client.GetAsync($"/api/albums/search?artist={new string('a', 201)}");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(Factory.Catalog.LastQuery, Is.Null);
        });
    }

    [Test]
    public async Task Search_returns_503_problem_details_when_the_catalogue_is_unavailable()
    {
        Factory.Catalog.Unavailable = true;

        var response = await Client.GetAsync("/api/albums/search?artist=Daft%20Punk");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var body = problem.GetRawText();
        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("title").GetString(), Is.EqualTo("Music catalogue unavailable"));
            Assert.That(body, Does.Not.Contain("Simulated outage"), "provider internals must not leak to clients");
            Assert.That(body, Does.Not.Contain("Exception"));
        });
    }

    [Test]
    public async Task Unknown_route_returns_404_problem_details()
    {
        var response = await Client.GetAsync("/api/nothing-here");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task Non_guid_user_id_returns_404()
    {
        var response = await Client.GetAsync("/api/users/not-a-guid/library");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
