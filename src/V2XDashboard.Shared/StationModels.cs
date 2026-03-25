namespace V2XDashboard.Shared;

public class StationProfileDto
{
    public string StationId { get; set; } = string.Empty;
    public string EntityType { get; set; } = "Unknown";
    public int? StationType { get; set; }
    public string? VehicleCategory { get; set; }
    public bool SupportsSecureComm { get; set; }
    public bool SupportsSigned { get; set; }
    public bool SupportsEncrypted { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public List<string> ObservedMessageTypes { get; set; } = new();
}

public class StationCapabilitiesSummaryDto
{
    public int TotalStations { get; set; }
    public int ObuStations { get; set; }
    public int RsuStations { get; set; }
    public int EventStations { get; set; }
    public int SecureStations { get; set; }
    public int SignedStations { get; set; }
    public int EncryptedStations { get; set; }
    public Dictionary<string, int> MessageTypeCoverage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
