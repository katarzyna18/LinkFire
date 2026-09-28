namespace Linkfire.MusicLibrary.Application.Catalog;

public static class MusicCatalogProviders
{
    /// <summary>
    /// Service key under which concrete catalogue adapters are registered. The unkeyed
    /// <see cref="IMusicCatalogProvider"/> is the composite that spans all of them.
    /// </summary>
    public const string LeafServiceKey = "catalog-provider";
}
