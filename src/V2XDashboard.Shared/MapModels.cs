namespace V2XDashboard.Shared;

public class MapEntityDto
{
    public string EntityType { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string? StationId { get; set; }
    public int? IntersectionId { get; set; }
    public string? IntersectionName { get; set; }
    public string? PublisherId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public DateTime GenerationTime { get; set; }
    public string? VehicleRole { get; set; }
    public int? StationType { get; set; }
    public bool IsSecureSigned { get; set; }
    public bool IsSecureEncrypted { get; set; }
}

public class CorrelationDto
{
    public int Id { get; set; }
    public int? SremId { get; set; }
    public int? SsemId { get; set; }
    public string? ObuStationId { get; set; }
    public string? RsuIntersectionId { get; set; }
    public string? RequestId { get; set; }
    public string CorrelationType { get; set; } = string.Empty;
    public double MatchConfidence { get; set; }
    public DateTime? SremTimestamp { get; set; }
    public DateTime? SsemTimestamp { get; set; }
    public int? TimeDeltaMs { get; set; }
    public string? RequestType { get; set; }
    public string? StatusCode { get; set; }
    public int? GrantedDuration { get; set; }
    public double? SremLatitude { get; set; }
    public double? SremLongitude { get; set; }
    public double? SsemLatitude { get; set; }
    public double? SsemLongitude { get; set; }
    public bool IsSecureSigned { get; set; }
    public bool IsSecureEncrypted { get; set; }
}

public class SremSsemMatchDto
{
    public int Id { get; set; }
    public int? SsemId { get; set; }
    public string? RequestId { get; set; }
    public string CorrelationType { get; set; } = string.Empty;
    public double MatchConfidence { get; set; }
    public int? SsemMessageId { get; set; }
    public string? StatusCode { get; set; }
    public int? GrantedDuration { get; set; }
    public DateTime? GenerationTime { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class MapConfigDto
{
    public string TileStyleUrl { get; set; } = string.Empty;
    public double DefaultCenterLatitude { get; set; }
    public double DefaultCenterLongitude { get; set; }
    public double DefaultZoom { get; set; }
    public double MinZoom { get; set; }
    public double MaxZoom { get; set; }
}

public sealed class MapVehicleSummaryQueryParams
{
    public string[]? VisibleLayers { get; set; }

    public DateTime? FromTime { get; set; }

    public DateTime? ToTime { get; set; }

    public bool? IsSecureSigned { get; set; }

    public bool? IsSecureEncrypted { get; set; }

    public int[]? StationTypes { get; set; }

    public string[]? VehicleRoles { get; set; }

    public double? MinLatitude { get; set; }

    public double? MaxLatitude { get; set; }

    public double? MinLongitude { get; set; }

    public double? MaxLongitude { get; set; }

    public int? TileZ { get; set; }

    public int? TileX { get; set; }

    public int? TileY { get; set; }
}

public sealed class MapVehicleFilterSummaryDto
{
    public int DistinctVehicleCount { get; set; }

    public string Scope { get; set; } = "Global";

    public IReadOnlyList<StationTypeShareDto> StationTypeShares { get; set; } = Array.Empty<StationTypeShareDto>();
}

public sealed class StationTypeShareDto
{
    public int StationType { get; set; }

    public int DistinctVehicles { get; set; }

    public double Percentage { get; set; }
}
