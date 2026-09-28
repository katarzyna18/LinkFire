using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.Application.Libraries;

public sealed record AddAlbumsResult(
    IReadOnlyList<SavedAlbum> Added,
    IReadOnlyList<CatalogAlbum> Skipped);
