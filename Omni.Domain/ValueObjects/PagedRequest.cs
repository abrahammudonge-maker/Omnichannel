namespace Omni.Domain.ValueObjects;

public sealed class PagedRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public string? Filter { get; init; }
}
