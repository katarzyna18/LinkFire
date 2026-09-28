using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Linkfire.MusicLibrary.Application.Catalog;
using Linkfire.MusicLibrary.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Linkfire.MusicLibrary.Infrastructure.Deezer;

public sealed class DeezerMusicCatalogProvider : IMusicCatalogProvider
{
    public const string Name = "deezer";

    private readonly HttpClient _httpClient;
    private readonly DeezerOptions _options;
    private readonly ILogger<DeezerMusicCatalogProvider> _logger;

    public DeezerMusicCatalogProvider(
        HttpClient httpClient,
        IOptions<DeezerOptions> options,
        ILogger<DeezerMusicCatalogProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => Name;

    public async Task<CatalogSearchResult> SearchAlbumsAsync(AlbumSearchQuery query, CancellationToken cancellationToken)
    {
        var requestUri = BuildSearchUri(query);

        DeezerSearchResponse? payload;
        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Deezer search returned HTTP {StatusCode}", (int)response.StatusCode);
                return Failed();
            }

            payload = await response.Content.ReadFromJsonAsync<DeezerSearchResponse>(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Deezer search failed with a network error");
            return Failed();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient signals its own timeout as a cancellation; a caller-initiated cancel is left alone.
            _logger.LogWarning(ex, "Deezer search timed out");
            return Failed();
        }
        catch (ExecutionRejectedException ex)
        {
            // Raised by the resilience pipeline: total timeout exceeded or circuit open.
            _logger.LogWarning(ex, "Deezer search rejected by the resilience pipeline");
            return Failed();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Deezer search returned a malformed response");
            return Failed();
        }

        if (payload is null)
        {
            _logger.LogWarning("Deezer search returned an empty body");
            return Failed();
        }

        if (payload.Error is not null)
        {
            // Deezer reports quota and API errors inside a 200 response.
            _logger.LogWarning(
                "Deezer search returned error {ErrorCode} ({ErrorType}): {ErrorMessage}",
                payload.Error.Code,
                payload.Error.Type,
                payload.Error.Message);
            return Failed();
        }

        var albums = (payload.Data ?? [])
            .Select(MapAlbum)
            .OfType<CatalogAlbum>()
            .ToList();

        return CatalogSearchResult.Success(Name, albums);
    }

    private string BuildSearchUri(AlbumSearchQuery query)
    {
        // Deezer documents fielded queries (artist:"..." album:"...") for /search, but /search/album
        // treats them as plain text and returns nothing for combined filters. Free text with both
        // terms is what actually ranks the right album first, so that is what we send.
        var terms = new[] { query.Artist, query.Album }.Where(t => t is not null);

        var q = Uri.EscapeDataString(string.Join(' ', terms));
        return $"search/album?q={q}&limit={_options.MaxResults}";
    }

    private CatalogAlbum? MapAlbum(DeezerAlbum album)
    {
        // Every result must be identifiable and linkable; anything else is skipped rather than
        // failing the whole search over one incomplete entry.
        if (album.Id <= 0 || string.IsNullOrWhiteSpace(album.Link))
        {
            _logger.LogDebug("Skipping Deezer album without id or link (id: {AlbumId})", album.Id);
            return null;
        }

        return new CatalogAlbum(
            Provider: Name,
            ProviderAlbumId: album.Id.ToString(CultureInfo.InvariantCulture),
            ArtistName: album.Artist?.Name?.Trim() ?? "Unknown artist",
            AlbumName: album.Title?.Trim() ?? "Untitled",
            CoverUrl: NullIfEmpty(album.CoverMedium) ?? NullIfEmpty(album.Cover),
            AlbumUrl: album.Link);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static CatalogSearchResult Failed() => CatalogSearchResult.Failed(Name);
}
