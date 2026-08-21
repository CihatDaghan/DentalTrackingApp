namespace Dental.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record PageRequest(int Page = 1, int PageSize = 25)
{
    public const int MaxPageSize = 100;
    public int EffectivePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
    public int Skip => (Math.Max(Page, 1) - 1) * EffectivePageSize;
}
