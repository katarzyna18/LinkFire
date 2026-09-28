using System.ComponentModel.DataAnnotations;

namespace Linkfire.MusicLibrary.Api.Contracts;

/// <summary>
/// [Required] accepts strings made only of spaces; this rejects them as well.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotWhiteSpaceAttribute : ValidationAttribute
{
    public NotWhiteSpaceAttribute()
        : base("The {0} field must not be blank.")
    {
    }

    public override bool IsValid(object? value) =>
        value is not string text || !string.IsNullOrWhiteSpace(text);
}
