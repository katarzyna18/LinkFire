using Linkfire.MusicLibrary.Application;
using Linkfire.MusicLibrary.Application.Catalog;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Linkfire.MusicLibrary.Api;

/// <summary>
/// Translates known application failures into ProblemDetails responses.
/// Anything unrecognised falls through to the default handler, which returns a generic 500
/// without exposing exception details.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = notFound.Message,
            },
            ConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicting update",
                Detail = conflict.Message,
            },
            MusicCatalogUnavailableException unavailable => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Music catalogue unavailable",
                Detail = $"The {unavailable.ProviderName} catalogue is temporarily unavailable. Please try again later.",
            },
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        // Provider failures are already logged with detail by the adapter; here we only record the outcome.
        _logger.LogInformation("Request failed with {StatusCode}: {ExceptionType}", problem.Status, exception.GetType().Name);

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
