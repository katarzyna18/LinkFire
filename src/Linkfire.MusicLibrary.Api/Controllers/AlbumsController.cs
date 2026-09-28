using System.ComponentModel.DataAnnotations;
using Linkfire.MusicLibrary.Api.Contracts;
using Linkfire.MusicLibrary.Application.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Linkfire.MusicLibrary.Api.Controllers;

[ApiController]
[Route("api/albums")]
public sealed class AlbumsController : ControllerBase
{
    private readonly IMusicCatalogProvider _catalog;

    public AlbumsController(IMusicCatalogProvider catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Searches the configured music catalogues by album and/or artist name.</summary>
    [HttpGet("search")]
    [ProducesResponseType<AlbumSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AlbumSearchResponse>> Search(
        [FromQuery, StringLength(200)] string? album,
        [FromQuery, StringLength(200)] string? artist,
        CancellationToken cancellationToken)
    {
        if (!AlbumSearchQuery.TryCreate(album, artist, out var query))
        {
            ModelState.AddModelError("query", "Provide at least one of 'album' or 'artist'.");
            return ValidationProblem(ModelState);
        }

        var result = await _catalog.SearchAlbumsAsync(query, cancellationToken);

        return result.IsUnavailable
            ? this.CatalogueUnavailable(result.FailedProviders)
            : AlbumSearchResponse.From(result);
    }
}
