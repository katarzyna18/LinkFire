using System.Diagnostics.CodeAnalysis;

namespace Linkfire.MusicLibrary.Application;

public enum ErrorKind
{
    NotFound,
    Conflict,
}

public sealed record Error(ErrorKind Kind, string Message)
{
    public static Error UserNotFound(Guid userId) =>
        new(ErrorKind.NotFound, $"User '{userId}' was not found.");

    public static Error SavedAlbumNotFound(Guid albumId) =>
        new(ErrorKind.NotFound, $"Album '{albumId}' was not found in the library.");

    public static Error Conflict(string message) => new(ErrorKind.Conflict, message);
}

/// <summary>
/// Outcome of a use case. Expected failures (unknown user, lost race) are values, not exceptions,
/// so callers have to look at them and the API layer can map them without a global handler.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;

    private Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure: {Error.Message}");

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}

public readonly struct Result
{
    private Result(Error? error)
    {
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public Error? Error { get; }

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}
