namespace Linkfire.MusicLibrary.Infrastructure.Deezer;

public sealed class DeezerOptions
{
    public const string SectionName = "Deezer";

    public string BaseUrl { get; set; } = "https://api.deezer.com/";

    /// <summary>Timeout for a single attempt; the resilience pipeline may retry within the total timeout.</summary>
    public int AttemptTimeoutSeconds { get; set; } = 5;

    public int TotalTimeoutSeconds { get; set; } = 15;

    public int MaxRetries { get; set; } = 2;

    public int MaxResults { get; set; } = 25;
}
