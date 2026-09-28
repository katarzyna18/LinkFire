namespace Linkfire.MusicLibrary.Application;

public sealed record PageRequest
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public PageRequest(int page = 1, int pageSize = DefaultPageSize)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page numbers start at 1.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"Page size must be between 1 and {MaxPageSize}.");
        }

        Page = page;
        PageSize = pageSize;
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;
}
