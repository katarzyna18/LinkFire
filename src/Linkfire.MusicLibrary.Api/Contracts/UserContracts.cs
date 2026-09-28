using System.ComponentModel.DataAnnotations;
using Linkfire.MusicLibrary.Domain;

namespace Linkfire.MusicLibrary.Api.Contracts;

public sealed record CreateUserRequest
{
    [Required(AllowEmptyStrings = false)]
    [NotWhiteSpace]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;
}

public sealed record UserResponse(Guid Id, string Name, Guid LibraryId)
{
    public static UserResponse From(User user) => new(user.Id, user.Name, user.Library.Id);
}
