using System.ComponentModel.DataAnnotations;
using Linkfire.MusicLibrary.Application.Libraries;
using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.Api.Contracts;

public sealed record AddAlbumsRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one album is required.")]
    public List<AddAlbumRequest> Albums { get; init; } = [];
}

public sealed record AddAlbumRequest
{
    [Required(AllowEmptyStrings = false)]
    [NotWhiteSpace]
    [MaxLength(50)]
    public string Provider { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [NotWhiteSpace]
    [MaxLength(200)]
    public string ProviderAlbumId { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [NotWhiteSpace]
    [MaxLength(500)]
    public string ArtistName { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [NotWhiteSpace]
    [MaxLength(500)]
    public string AlbumName { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? CoverUrl { get; init; }

    [Required(AllowEmptyStrings = false)]
    [NotWhiteSpace]
    [MaxLength(2000)]
    public string AlbumUrl { get; init; } = string.Empty;

    public CatalogAlbum ToCatalogAlbum() => new(
        Provider.Trim(),
        ProviderAlbumId.Trim(),
        ArtistName.Trim(),
        AlbumName.Trim(),
        string.IsNullOrWhiteSpace(CoverUrl) ? null : CoverUrl.Trim(),
        AlbumUrl.Trim());
}

public sealed record SavedAlbumResponse(
    Guid Id,
    string Provider,
    string ProviderAlbumId,
    string ArtistName,
    string AlbumName,
    string? CoverUrl,
    string AlbumUrl,
    DateTime AddedAt)
{
    public static SavedAlbumResponse From(SavedAlbum album) => new(
        album.Id,
        album.Provider,
        album.ProviderAlbumId,
        album.ArtistName,
        album.AlbumName,
        album.CoverUrl,
        album.AlbumUrl,
        album.AddedAt);
}

public sealed record LibraryResponse(
    Guid Id,
    Guid UserId,
    IReadOnlyList<SavedAlbumResponse> Albums,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static LibraryResponse From(LibraryPage page) => new(
        page.LibraryId,
        page.UserId,
        page.Albums.Select(SavedAlbumResponse.From).ToList(),
        page.Page,
        page.PageSize,
        page.TotalCount);
}

/// <summary>
/// Adding is idempotent: albums already in the library are listed under <see cref="Skipped"/>.
/// </summary>
public sealed record AddAlbumsResponse(
    IReadOnlyList<SavedAlbumResponse> Added,
    IReadOnlyList<AlbumSearchResultResponse> Skipped)
{
    public static AddAlbumsResponse From(AddAlbumsResult result) => new(
        result.Added.Select(SavedAlbumResponse.From).ToList(),
        result.Skipped.Select(AlbumSearchResultResponse.From).ToList());
}
