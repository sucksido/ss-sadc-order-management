namespace SadcOms.Application.Common;

/// <summary>
/// Normalised paging parameters. The brief caps page size at 100; we clamp rather than
/// reject so a client asking for 1,000 still gets a sensible response instead of an error,
/// and a non-positive page is coerced to the first page.
/// </summary>
public readonly record struct PageRequest
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public PageRequest(int? page, int? pageSize)
    {
        Page = page is > 0 ? page.Value : 1;
        PageSize = pageSize switch
        {
            null or <= 0 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize.Value
        };
    }

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (Page - 1) * PageSize;
}
