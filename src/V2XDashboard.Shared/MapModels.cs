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
