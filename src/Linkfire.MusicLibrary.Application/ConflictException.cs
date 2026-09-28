namespace Linkfire.MusicLibrary.Application;

/// <summary>
/// Raised when a write lost a race with a concurrent change to the same library.
/// The request is safe to retry.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
