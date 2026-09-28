using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Linkfire.MusicLibrary.ApiTests;

/// <summary>
/// End-to-end walk through the main user story: create a user, search, add albums, read the library,
/// add a duplicate, remove an album.
/// </summary>
[TestFixture]
public class LibraryFlowTests : ApiTestBase
{
    [Test]
    public async Task Creating_a_user_returns_201_with_the_user_and_library_ids()
    {
        var response = await Client.PostAsJsonAsync("/api/users", new { name = "Kasia" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var user = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Multiple(() =>
        {
            Assert.That(user.GetProperty("name").GetString(), Is.EqualTo("Kasia"));
            Assert.That(user.GetProperty("id").GetGuid(), Is.Not.EqualTo(Guid.Empty));
            Assert.That(user.GetProperty("libraryId").GetGuid(), Is.Not.EqualTo(Guid.Empty));
            Assert.That(response.Headers.Location!.ToString(), Does.EndWith($"/api/users/{user.GetProperty("id").GetGuid()}"));
        });
    }

    [Test]
    public async Task Search_returns_normalized_results_from_the_catalogue_provider()
    {
        Factory.Catalog.Results = [Discovery, Homework];

        var results = await Client.GetFromJsonAsync<JsonElement>("/api/albums/search?album=Discovery&artist=Daft%20Punk", Json);

        Assert.Multiple(() =>
        {
            Assert.That(Factory.Catalog.LastQuery!.Album, Is.EqualTo("Discovery"));
            Assert.That(Factory.Catalog.LastQuery.Artist, Is.EqualTo("Daft Punk"));
            Assert.That(results.GetArrayLength(), Is.EqualTo(2));
            Assert.That(results[0].GetProperty("provider").GetString(), Is.EqualTo("fake"));
            Assert.That(results[0].GetProperty("providerAlbumId").GetString(), Is.EqualTo("302127"));
            Assert.That(results[0].GetProperty("artistName").GetString(), Is.EqualTo("Daft Punk"));
            Assert.That(results[0].GetProperty("albumName").GetString(), Is.EqualTo("Discovery"));
            Assert.That(results[0].GetProperty("coverUrl").GetString(), Is.EqualTo("https://cdn.example/discovery.jpg"));
            Assert.That(results[0].GetProperty("albumUrl").GetString(), Is.EqualTo("https://music.example/album/302127"));
            Assert.That(results[1].GetProperty("coverUrl").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public async Task User_can_add_albums_from_search_results_read_the_library_and_remove_an_album()
    {
        var userId = await CreateUserAsync();

        // Add two albums in one request.
        var addResponse = await Client.PostAsJsonAsync($"/api/users/{userId}/library/albums", AddAlbumsBody(Discovery, Homework));
        Assert.That(addResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var addResult = await addResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Multiple(() =>
        {
            Assert.That(addResult.GetProperty("added").GetArrayLength(), Is.EqualTo(2));
            Assert.That(addResult.GetProperty("skipped").GetArrayLength(), Is.EqualTo(0));
        });

        // The library now contains both.
        var library = await Client.GetFromJsonAsync<JsonElement>($"/api/users/{userId}/library", Json);
        var albums = library.GetProperty("albums");
        Assert.Multiple(() =>
        {
            Assert.That(library.GetProperty("userId").GetGuid(), Is.EqualTo(userId));
            Assert.That(albums.GetArrayLength(), Is.EqualTo(2));
            Assert.That(albums[0].GetProperty("albumName").GetString(), Is.EqualTo("Discovery"));
            Assert.That(albums[0].GetProperty("artistName").GetString(), Is.EqualTo("Daft Punk"));
            Assert.That(albums[0].GetProperty("albumUrl").GetString(), Is.EqualTo("https://music.example/album/302127"));
        });

        // Adding the same album again is idempotent.
        var duplicateResponse = await Client.PostAsJsonAsync($"/api/users/{userId}/library/albums", AddAlbumsBody(Discovery));
        Assert.That(duplicateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var duplicateResult = await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Multiple(() =>
        {
            Assert.That(duplicateResult.GetProperty("added").GetArrayLength(), Is.EqualTo(0));
            Assert.That(duplicateResult.GetProperty("skipped").GetArrayLength(), Is.EqualTo(1));
        });

        // Remove one album.
        var savedAlbumId = albums[0].GetProperty("id").GetGuid();
        var deleteResponse = await Client.DeleteAsync($"/api/users/{userId}/library/albums/{savedAlbumId}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var libraryAfter = await Client.GetFromJsonAsync<JsonElement>($"/api/users/{userId}/library", Json);
        var remaining = libraryAfter.GetProperty("albums");
        Assert.Multiple(() =>
        {
            Assert.That(remaining.GetArrayLength(), Is.EqualTo(1));
            Assert.That(remaining[0].GetProperty("albumName").GetString(), Is.EqualTo("Homework"));
        });

        // Removing it a second time is a 404.
        var deleteAgain = await Client.DeleteAsync($"/api/users/{userId}/library/albums/{savedAlbumId}");
        Assert.That(deleteAgain.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Libraries_are_isolated_per_user()
    {
        var first = await CreateUserAsync("First");
        var second = await CreateUserAsync("Second");

        await Client.PostAsJsonAsync($"/api/users/{first}/library/albums", AddAlbumsBody(Discovery));

        var secondLibrary = await Client.GetFromJsonAsync<JsonElement>($"/api/users/{second}/library", Json);

        Assert.That(secondLibrary.GetProperty("albums").GetArrayLength(), Is.EqualTo(0));
    }
}
