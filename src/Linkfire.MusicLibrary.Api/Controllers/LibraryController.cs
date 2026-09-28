using Linkfire.MusicLibrary.Api.Contracts;
using Linkfire.MusicLibrary.Application.Libraries;
using Microsoft.AspNetCore.Mvc;

namespace Linkfire.MusicLibrary.Api.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/library")]
[Produces("application/json")]
public sealed class LibraryController : ControllerBase
{
    private readonly LibraryService _libraryService;

    public LibraryController(LibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    [HttpGet]
    [ProducesResponseType<LibraryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LibraryResponse>> Get(Guid userId, CancellationToken cancellationToken)
    {
        var library = await _libraryService.GetLibraryAsync(userId, cancellationToken);

        return LibraryResponse.From(library);
    }

    /// <summary>Adds one or more albums (typically taken from search results) to the library.</summary>
    [HttpPost("albums")]
    [ProducesResponseType<AddAlbumsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddAlbumsResponse>> AddAlbums(
        Guid userId,
        AddAlbumsRequest request,
        CancellationToken cancellationToken)
    {
        var albums = request.Albums.Select(a => a.ToCatalogAlbum()).ToList();

        var result = await _libraryService.AddAlbumsAsync(userId, albums, cancellationToken);

        return AddAlbumsResponse.From(result);
    }

    [HttpDelete("albums/{albumId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAlbum(Guid userId, Guid albumId, CancellationToken cancellationToken)
    {
        await _libraryService.RemoveAlbumAsync(userId, albumId, cancellationToken);

        return NoContent();
    }
}
