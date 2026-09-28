using System.Text.Json.Serialization;

namespace Linkfire.MusicLibrary.Infrastructure.Deezer;

// Wire format of https://api.deezer.com/search/album. Kept internal so nothing outside this
// folder can take a dependency on Deezer's field names.
internal sealed class DeezerSearchResponse
{
    [JsonPropertyName("data")]
    public List<DeezerAlbum>? Data { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("error")]
    public DeezerError? Error { get; set; }
}

internal sealed class DeezerAlbum
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("cover_medium")]
    public string? CoverMedium { get; set; }

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    [JsonPropertyName("artist")]
    public DeezerArtist? Artist { get; set; }
}

internal sealed class DeezerArtist
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class DeezerError
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }
}
