using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.Application.Catalog;

/// <summary>
/// Outcome of a catalogue search. A provider either contributes albums or reports itself as failed;
/// a composite merges several of these, so a partially degraded search is still a usable answer.
/// </summary>
public sealed record CatalogSearchResult(
    IReadOnlyList<CatalogAlbum> Albums,
    IReadOnlyList<string> SucceededProviders,
    IReadOnlyList<string> FailedProviders)
{
    /// <summary>True when no provider could answer at all.</summary>
    public bool IsUnavailable => SucceededProviders.Count == 0;

    public static CatalogSearchResult Success(string provider, IReadOnlyList<CatalogAlbum> albums) =>
        new(albums, [provider], []);

    public static CatalogSearchResult Failed(string provider) =>
        new([], [], [provider]);

    public static CatalogSearchResult Merge(IEnumerable<CatalogSearchResult> results)
    {
        var list = results.ToList();
        return new CatalogSearchResult(
            list.SelectMany(r => r.Albums).ToList(),
            list.SelectMany(r => r.SucceededProviders).ToList(),
            list.SelectMany(r => r.FailedProviders).ToList());
    }
}
