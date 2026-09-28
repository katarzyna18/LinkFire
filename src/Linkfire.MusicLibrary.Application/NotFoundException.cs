namespace Linkfire.MusicLibrary.Application;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public static NotFoundException User(Guid userId) => new($"User '{userId}' was not found.");

    public static NotFoundException SavedAlbum(Guid albumId) => new($"Album '{albumId}' was not found in the library.");
}
