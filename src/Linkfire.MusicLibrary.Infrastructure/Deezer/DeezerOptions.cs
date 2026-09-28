namespace Linkfire.MusicLibrary.Infrastructure.Deezer;

public sealed class DeezerOptions
{
    public const string SectionName = "Deezer";

    public string BaseUrl { get; set; } = "https://api.deezer.com/";

    public int TimeoutSeconds { get; set; } = 5;

    public int MaxResults { get; set; } = 25;
}
