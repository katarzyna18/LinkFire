using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.Application.Libraries;

public sealed record LibraryPage(
    Guid LibraryId,
    Guid UserId,
    IReadOnlyList<SavedAlbum> Albums,
    int Page,
    int PageSize,
    int TotalCount);
