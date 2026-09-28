namespace Linkfire.MusicLibrary.Domain;

public class User
{
    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Library Library { get; private set; } = null!;

    // A user always owns exactly one library, so the library is created together with the user.
    public static User Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("User name is required.", nameof(name));
        }

        var userId = Guid.NewGuid();

        return new User
        {
            Id = userId,
            Name = name.Trim(),
            Library = Library.CreateFor(userId),
        };
    }
}
