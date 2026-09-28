using Linkfire.MusicLibrary.Application.Persistence;
using Linkfire.MusicLibrary.Domain;
using Microsoft.EntityFrameworkCore;

namespace Linkfire.MusicLibrary.Application.Users;

public sealed class UserService
{
    private readonly IMusicLibraryDbContext _dbContext;

    public UserService(IMusicLibraryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var user = User.Create(name);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<Result<User>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Library)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is null ? Error.UserNotFound(userId) : user;
    }
}
