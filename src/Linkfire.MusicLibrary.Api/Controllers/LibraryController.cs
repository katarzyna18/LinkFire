using System.ComponentModel.DataAnnotations;
using Linkfire.MusicLibrary.Api.Contracts;
using Linkfire.MusicLibrary.Application;
using Linkfire.MusicLibrary.Application.Libraries;
using Microsoft.AspNetCore.Mvc;

namespace Linkfire.MusicLibrary.Api.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/library")]
public sealed class LibraryController : ControllerBase
{
    private readonly LibraryService _libraryService;

    public LibraryController(LibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    /// <summary>Returns one page of the user's library, oldest additions first.</summary>
    [HttpGet]
    [ProducesResponseType<LibraryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LibraryResponse>> Get(
        Guid userId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _libraryService.GetLibraryPageAsync(userId, new PageRequest(page, pageSize), cancellationToken);

        return result.IsSuccess ? LibraryResponse.From(result.Value) : this.ToProblem(result.Error);
    }

    /// <summary>Adds one or more albums (typically taken from search results) to the library.</summary>
    [HttpPost("albums")]
    [ProducesResponseType<AddAlbumsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddAlbumsResponse>> AddAlbums(
        Guid userId,
        AddAlbumsRequest request,
        CancellationToken cancellationToken)
    {
        var albums = request.Albums.Select(a => a.ToCatalogAlbum()).ToList();

        var result = await _libraryService.AddAlbumsAsync(userId, albums, cancellationToken);

        return result.IsSuccess ? AddAlbumsResponse.From(result.Value) : this.ToProblem(result.Error);
    }

    [HttpDelete("albums/{albumId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveAlbum(Guid userId, Guid albumId, CancellationToken cancellationToken)
    {
        var result = await _libraryService.RemoveAlbumAsync(userId, albumId, cancellationToken);

        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
