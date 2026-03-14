namespace V2XDashboard.Shared;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class MessageListItemDto
{
    public string MessageType { get; set; } = string.Empty;
    public DateTime GenerationTime { get; set; }
    public string StationLabel { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Detail { get; set; } = string.Empty;
}