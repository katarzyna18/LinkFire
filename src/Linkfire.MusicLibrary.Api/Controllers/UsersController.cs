using Linkfire.MusicLibrary.Api.Contracts;
using Linkfire.MusicLibrary.Application.Users;
using Microsoft.AspNetCore.Mvc;

namespace Linkfire.MusicLibrary.Api.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    /// <summary>Creates a user together with their (single) library.</summary>
    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.CreateAsync(request.Name, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { userId = user.Id }, UserResponse.From(user));
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userService.GetAsync(userId, cancellationToken);

        return UserResponse.From(user);
    }
}
