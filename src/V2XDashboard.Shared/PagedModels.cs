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

public class MessageCountsDto
{
    public int TotalPackets { get; set; }
    public int CAM { get; set; }
    public int DENM { get; set; }
    public int MAPEM { get; set; }
    public int SPATEM { get; set; }
    public int SREM { get; set; }
    public int SSEM { get; set; }
    public int TotalCorrelations { get; set; }
}

public class SecurityMetadataSummaryDto
{
    public int TotalPackets { get; set; }
    public int SignedPackets { get; set; }
    public int EncryptedPackets { get; set; }
    public int SecurePackets { get; set; }
    public int DistinctSigners { get; set; }
    public int DistinctCertificates { get; set; }
}

public class DistinctVehicleWindowBucketDto
{
    public DateTime BucketStart { get; set; }
    public int DistinctVehicles { get; set; }
}

public class DistinctVehicleWindowStatsDto
{
    public int WindowMinutes { get; set; } = 15;
    public int TotalDistinctVehicleWindows { get; set; }
    public IReadOnlyList<DistinctVehicleWindowBucketDto> Buckets { get; set; } = Array.Empty<DistinctVehicleWindowBucketDto>();
}
