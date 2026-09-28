using System.ComponentModel.DataAnnotations;
using Linkfire.MusicLibrary.Api.Contracts;
using Linkfire.MusicLibrary.Application.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Linkfire.MusicLibrary.Api.Controllers;

[ApiController]
[Route("api/albums")]
[Produces("application/json")]
public sealed class AlbumsController : ControllerBase
{
    private readonly IMusicCatalogProvider _catalogProvider;

    public AlbumsController(IMusicCatalogProvider catalogProvider)
    {
        _catalogProvider = catalogProvider;
    }

    /// <summary>Searches the external music catalogue by album and/or artist name.</summary>
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<AlbumSearchResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<AlbumSearchResultResponse>>> Search(
        [FromQuery, StringLength(200)] string? album,
        [FromQuery, StringLength(200)] string? artist,
        CancellationToken cancellationToken)
    {
        if (!AlbumSearchQuery.TryCreate(album, artist, out var query))
        {
            ModelState.AddModelError("query", "Provide at least one of 'album' or 'artist'.");
            return ValidationProblem(ModelState);
        }

        var results = await _catalogProvider.SearchAlbumsAsync(query, cancellationToken);

        return Ok(results.Select(AlbumSearchResultResponse.From).ToList());
    }
}
