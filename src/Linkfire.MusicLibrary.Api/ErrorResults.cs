using Linkfire.MusicLibrary.Application;
using Microsoft.AspNetCore.Mvc;

namespace Linkfire.MusicLibrary.Api;

/// <summary>Single place where application errors become HTTP ProblemDetails.</summary>
public static class ErrorResults
{
    public static ActionResult ToProblem(this ControllerBase controller, Error error) => error.Kind switch
    {
        ErrorKind.NotFound => controller.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Resource not found",
            detail: error.Message),
        ErrorKind.Conflict => controller.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflicting update",
            detail: error.Message),
        _ => controller.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    public static ActionResult CatalogueUnavailable(this ControllerBase controller, IReadOnlyList<string> providers) =>
        controller.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Music catalogue unavailable",
            detail: $"No music catalogue could be reached ({string.Join(", ", providers)}). Please try again later.");
}
