namespace V2XDashboard.Shared;

public sealed class TileFilterParams
{
    public DateTime? FromTime { get; set; }

    public DateTime? ToTime { get; set; }

    public bool? IsSecureSigned { get; set; }

    public bool? IsSecureEncrypted { get; set; }

    public int[]? StationTypes { get; set; }

    public string[]? VehicleRoles { get; set; }
}

public sealed class TileCacheDiagnosticsDto
{
    public required DateTimeOffset GeneratedAt { get; init; }

    public required IReadOnlyDictionary<string, int> LayerGenerations { get; init; }
}

public sealed class TileCacheInvalidateResultDto
{
    public required string Scope { get; init; }

    public string? Layer { get; init; }

    public required DateTimeOffset InvalidatedAt { get; init; }

    public required IReadOnlyDictionary<string, int> LayerGenerations { get; init; }
}

public static class TileLayerNames
{
    public const string Cam = "CAM";
    public const string Denm = "DENM";
    public const string Mapem = "MAPEM";
    public const string Spatem = "SPATEM";
    public const string Srem = "SREM";
    public const string Ssem = "SSEM";
    public const string Correlations = "Correlations";

    private static readonly string[] AllLayers =
    [
        Cam,
        Denm,
        Mapem,
        Spatem,
        Srem,
        Ssem,
        Correlations
    ];

    public static bool IsValid(string? layerName)
    {
        return !string.IsNullOrWhiteSpace(layerName)
            && AllLayers.Any(value => string.Equals(value, layerName, StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string layerName)
    {
        return layerName.Trim().ToUpperInvariant();
    }
}