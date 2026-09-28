using System.Net.Http.Json;
using System.Text.Json;
using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.ApiTests;

public abstract class ApiTestBase
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected static readonly CatalogAlbum Discovery = new(
        "fake", "302127", "Daft Punk", "Discovery", "https://cdn.example/discovery.jpg", "https://music.example/album/302127");

    protected static readonly CatalogAlbum Homework = new(
        "fake", "302128", "Daft Punk", "Homework", null, "https://music.example/album/302128");

    protected MusicLibraryApiFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    [SetUp]
    public void SetUpFactory()
    {
        Factory = new MusicLibraryApiFactory();
        Client = Factory.CreateClient();
    }

    [TearDown]
    public void TearDownFactory()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    protected async Task<Guid> CreateUserAsync(string name = "Kasia")
    {
        var response = await Client.PostAsJsonAsync("/api/users", new { name });
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return user.GetProperty("id").GetGuid();
    }

    protected static object AddAlbumsBody(params CatalogAlbum[] albums) => new
    {
        albums = albums.Select(a => new
        {
            provider = a.Provider,
            providerAlbumId = a.ProviderAlbumId,
            artistName = a.ArtistName,
            albumName = a.AlbumName,
            coverUrl = a.CoverUrl,
            albumUrl = a.AlbumUrl,
        }),
    };
}
