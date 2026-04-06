using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader;

public class V2XMessageDecoder :
    IV2XMessageDecoder,
    ICamDecoder,
    IDenmDecoder,
    IMapemDecoder,
    ISpatemDecoder,
    ISremDecoder,
    ISsemDecoder
{
    private static readonly ConditionalWeakTable<JsonDocument, Dictionary<string, JsonElement>> PropertyIndexCache = new();

    public CAM DecodeCAM(Packet packet)
    {
        using var payloadJson = TryParsePayloadJson(packet.Payload);

        var rawLatitude = GetDouble(payloadJson, "latitude", "lat", "its.latitude");
        var rawLongitude = GetDouble(payloadJson, "longitude", "lon", "lng", "its.longitude");
        var rawAltitude = GetDouble(payloadJson, "altitude", "elevation", "its.altitudeValue");
        var rawSpeed = GetDouble(payloadJson, "speed", "speedValue", "its.speedValue");
        var rawHeading = GetDouble(payloadJson, "heading", "course", "its.headingValue");
        var rawAcceleration = GetDouble(payloadJson, "acceleration", "longitudinalAcceleration");
        var rawCurvature = GetDouble(payloadJson, "curvature", "its.curvatureValue");
        var rawYawRate = GetDouble(payloadJson, "yawRate", "its.yawRateValue");
        var rawLateralAcceleration = GetDouble(payloadJson, "lateralAcceleration");
        var rawVerticalAcceleration = GetDouble(payloadJson, "verticalAcceleration");

        var latitude = ScaleCoordinate(rawLatitude);
        var longitude = ScaleCoordinate(rawLongitude);
        var altitude = ScaleAltitude(rawAltitude);
        var speed = mapemSpeedScaler(rawSpeed);
        var heading = ScaleHeading(rawHeading);
        var acceleration = ScaleAcceleration(rawAcceleration);
        var curvature = ScaleCurvature(rawCurvature);
        var yawRate = ScaleYawRate(rawYawRate);
        var lateralAcceleration = ScaleLateralAcceleration(rawLateralAcceleration);
        var verticalAcceleration = ScaleVerticalAcceleration(rawVerticalAcceleration);

        var stationType = GetInt(payloadJson, "stationType", fallback: 0);
        var vehicleRole = GetString(payloadJson, "vehicleRole", fallback: "unknown");
        var decodeStatus = DetermineCamDecodeStatus(latitude, longitude, speed, heading, stationType);
        var vehicleLength = DmtoM(GetDouble(payloadJson, "vehicleLength"));

        return new CAM
        {
            PacketId = packet.Id,
            GenerationTime = packet.Timestamp,
            StationId = ResolveStationId(packet, payloadJson),
            IsSecureSigned = packet.IsSecureSigned,
            IsSecureEncrypted = packet.IsSecureEncrypted,
            SecurityProtocol = packet.SecurityProtocol,
            SignerId = packet.SignerId,
            CertificateId = packet.CertificateId,
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude,
            Speed = speed,
            Heading = heading,
            StationType = stationType,
            VehicleRole = string.IsNullOrWhiteSpace(vehicleRole) ? "unknown" : vehicleRole,
            Acceleration = acceleration,
            Curvature = curvature,
            YawRate = yawRate,
            LateralAcceleration = lateralAcceleration,
            VerticalAcceleration = verticalAcceleration,
            DecodeStatus = decodeStatus,
            VehicleLength = vehicleLength,
            VehicleWidth = DmtoM(GetDouble(payloadJson, "vehicleWidth"))
        };
    }

    public DENM DecodeDENM(Packet packet)
    {
        using var payloadJson = TryParsePayloadJson(packet.Payload);

        var rawLatitude = GetDouble(payloadJson, "latitude", "lat");
        var rawLongitude = GetDouble(payloadJson, "longitude", "lon", "lng");
        var rawAltitude = GetDouble(payloadJson, "altitude", "elevation");

        var latitude = ScaleCoordinate(rawLatitude);
        var longitude = ScaleCoordinate(rawLongitude);
        var altitude = ScaleAltitude(rawAltitude);

        return new DENM
        {
            PacketId = packet.Id,
            GenerationTime = packet.Timestamp,
            StationId = ResolveStationId(packet, payloadJson),
            IsSecureSigned = packet.IsSecureSigned,
            IsSecureEncrypted = packet.IsSecureEncrypted,
            SecurityProtocol = packet.SecurityProtocol,
            SignerId = packet.SignerId,
            CertificateId = packet.CertificateId,
            CauseCode = GetString(payloadJson, "causeCode", fallback: "unknown"),
            DetectionTime = GetDateTime(payloadJson, "detectionTime", packet.Timestamp),
            ReferenceTime = GetDateTime(payloadJson, "referenceTime", packet.Timestamp),
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude,
            RelevanceTrafficDirection = GetInt(payloadJson, "relevanceTrafficDirection", fallback: 0),
            ValidityDuration = GetBool(payloadJson, "validityDuration", fallback: true),
            StationType = GetInt(payloadJson, "relevanceStationType", fallback: 0),
            AwarenessTrafficDirection = GetInt(payloadJson, "awarenessTrafficDirection", fallback: 0),
            OriginalStationType = GetInt(payloadJson, "denmOriginalStationId", fallback: 0)
        };
    }

    public MAPEM DecodeMAPEM(Packet packet)
    {
        using var payloadJson = TryParsePayloadJson(packet.Payload);

        var rawLatitude = GetDouble(payloadJson, "latitude", "lat");
        var rawLongitude = GetDouble(payloadJson, "longitude", "lon", "lng");

        var latitude = ScaleCoordinate(rawLatitude);
        var longitude = ScaleCoordinate(rawLongitude);

        return new MAPEM
        {
            PacketId = packet.Id,
            GenerationTime = packet.Timestamp,
            StationId = ResolveStationId(packet, payloadJson),
            IsSecureSigned = packet.IsSecureSigned,
            IsSecureEncrypted = packet.IsSecureEncrypted,
            SecurityProtocol = packet.SecurityProtocol,
            SignerId = packet.SignerId,
            CertificateId = packet.CertificateId,
            IntersectionId = GetInt(payloadJson, "intersectionId"),
            IntersectionName = GetString(payloadJson, "intersectionName"),
            Latitude = latitude,
            Longitude = longitude,
            LaneCount = GetInt(payloadJson, "laneCount"),
            RoadWidth = CmtoM(GetDouble(payloadJson, "roadWidth")),
            SpeedLimit = mapemSpeedScaler(GetDouble(payloadJson, "speedLimit")).ToString(CultureInfo.InvariantCulture),
            MapVersion = GetString(payloadJson, "mapVersion", fallback: "1.0"),
            PublisherId = GetString(payloadJson, "publisherId")
        };
    }

    public SPATEM DecodeSPATEM(Packet packet)
    {
        using var payloadJson = TryParsePayloadJson(packet.Payload);

        var rawLatitude = GetDouble(payloadJson, "latitude", "lat");
        var rawLongitude = GetDouble(payloadJson, "longitude", "lon", "lng");

        var latitude = ScaleCoordinate(rawLatitude);
        var longitude = ScaleCoordinate(rawLongitude);

        return new SPATEM
        {
            PacketId = packet.Id,
            GenerationTime = packet.Timestamp,
            StationId = ResolveStationId(packet, payloadJson),
            IsSecureSigned = packet.IsSecureSigned,
            IsSecureEncrypted = packet.IsSecureEncrypted,
            SecurityProtocol = packet.SecurityProtocol,
            SignerId = packet.SignerId,
            CertificateId = packet.CertificateId,
            IntersectionId = GetInt(payloadJson, "intersectionId"),
            IntersectionName = GetString(payloadJson, "intersectionName"),
            Latitude = latitude,
            Longitude = longitude,
            CurrentPhase = GetSpatemSignalGroup(payloadJson, 0),
            PhaseState = GetSpatemEventState(payloadJson, 0, "unknown"),
            ConnectionManeuverAssistId = GetSpatemConnectionManeuverAssistId(payloadJson, 0, 0),
            Phase0SignalGroup = GetSpatemSignalGroup(payloadJson, 0),
            Phase1SignalGroup = GetSpatemSignalGroup(payloadJson, 1),
            Phase2SignalGroup = GetSpatemSignalGroup(payloadJson, 2),
            Phase3SignalGroup = GetSpatemSignalGroup(payloadJson, 3),
            Phase4SignalGroup = GetSpatemSignalGroup(payloadJson, 4),
            Phase5SignalGroup = GetSpatemSignalGroup(payloadJson, 5),
            Phase0EventState = GetSpatemEventState(payloadJson, 0),
            Phase1EventState = GetSpatemEventState(payloadJson, 1),
            Phase2EventState = GetSpatemEventState(payloadJson, 2),
            Phase3EventState = GetSpatemEventState(payloadJson, 3),
            Phase4EventState = GetSpatemEventState(payloadJson, 4),
            Phase5EventState = GetSpatemEventState(payloadJson, 5),
            Phase0ConnectionManeuverAssistId0 = GetSpatemConnectionManeuverAssistId(payloadJson, 0, 0),
            Phase0ConnectionManeuverAssistId1 = GetSpatemConnectionManeuverAssistId(payloadJson, 0, 1),
            Phase1ConnectionManeuverAssistId0 = GetSpatemConnectionManeuverAssistId(payloadJson, 1, 0),
            Phase1ConnectionManeuverAssistId1 = GetSpatemConnectionManeuverAssistId(payloadJson, 1, 1),
            Phase2ConnectionManeuverAssistId0 = GetSpatemConnectionManeuverAssistId(payloadJson, 2, 0),
            Phase2ConnectionManeuverAssistId1 = GetSpatemConnectionManeuverAssistId(payloadJson, 2, 1),
            Phase3ConnectionManeuverAssistId0 = GetSpatemConnectionManeuverAssistId(payloadJson, 3, 0),
            Phase3ConnectionManeuverAssistId1 = GetSpatemConnectionManeuverAssistId(payloadJson, 3, 1),
            Phase4ConnectionManeuverAssistId0 = GetSpatemConnectionManeuverAssistId(payloadJson, 4, 0),
            Phase4ConnectionManeuverAssistId1 = GetSpatemConnectionManeuverAssistId(payloadJson, 4, 1),
            Phase5ConnectionManeuverAssistId0 = GetSpatemConnectionManeuverAssistId(payloadJson, 5, 0),
            Phase5ConnectionManeuverAssistId1 = GetSpatemConnectionManeuverAssistId(payloadJson, 5, 1),
            PublisherId = GetString(payloadJson, "publisherId")
        };
    }

    public SREM DecodeSREM(Packet packet)
    {
        using var payloadJson = TryParsePayloadJson(packet.Payload);

        var rawLatitude = GetDouble(payloadJson, "latitude", "lat");
        var rawLongitude = GetDouble(payloadJson, "longitude", "lon", "lng");

        var latitude = ScaleCoordinate(rawLatitude);
        var longitude = ScaleCoordinate(rawLongitude);

        return new SREM
        {
            PacketId = packet.Id,
            GenerationTime = packet.Timestamp,
            StationId = ResolveStationId(packet, payloadJson),
            IsSecureSigned = packet.IsSecureSigned,
            IsSecureEncrypted = packet.IsSecureEncrypted,
            SecurityProtocol = packet.SecurityProtocol,
            SignerId = packet.SignerId,
            CertificateId = packet.CertificateId,
            IntersectionName = GetString(payloadJson, "intersectionName"),
            IntersectionId = GetInt(payloadJson, "intersectionId"),
            Latitude = latitude,
            Longitude = longitude,
            RequestedPhase = GetInt(payloadJson, "requestedPhase"),
            VehicleType = GetString(payloadJson, "vehicleType"),
            RequestReason = GetString(payloadJson, "requestReason"),
            RequestId = GetString(payloadJson, "requestId"),
            RequestorId = GetString(payloadJson, "requestorId"),
            RequiredAccuracy = GetString(payloadJson, "requiredAccuracy"),
            InBoundLaneId = GetInt(payloadJson, "inBoundLane"),
            OutBoundLaneId = GetInt(payloadJson, "outBoundLane"),
            Heading = ScaleHeading(GetDouble(payloadJson, "heading")),
            Speed = mapemSpeedScaler(GetDouble(payloadJson, "tasSpeed")),
            TransmissionPower = GetInt(payloadJson, "transmissionPower"),
            RouteNames = GetString(payloadJson, "routeNames"),
            TransitSchedule = GetString(payloadJson, "transitSchedule"),
            RequestorName = GetString(payloadJson, "requestorName")
        };
    }

    public SSEM DecodeSSEM(Packet packet)
    {
        using var payloadJson = TryParsePayloadJson(packet.Payload);

        var rawLatitude = GetDouble(payloadJson, "latitude", "lat");
        var rawLongitude = GetDouble(payloadJson, "longitude", "lon", "lng");

        var latitude = ScaleCoordinate(rawLatitude);
        var longitude = ScaleCoordinate(rawLongitude);

        return new SSEM
        {
            PacketId = packet.Id,
            GenerationTime = packet.Timestamp,
            StationId = ResolveStationId(packet, payloadJson),
            IsSecureSigned = packet.IsSecureSigned,
            IsSecureEncrypted = packet.IsSecureEncrypted,
            SecurityProtocol = packet.SecurityProtocol,
            SignerId = packet.SignerId,
            CertificateId = packet.CertificateId,
            IntersectionName = GetString(payloadJson, "intersectionName"),
            Latitude = latitude,
            Longitude = longitude,
            IntersectionId = GetInt(payloadJson, "intersectionId"),
            StatusCode = GetString(payloadJson, "statusCode", fallback: "pending"),
            GrantedDuration = GetInt(payloadJson, "grantedDuration"),
            RequestIdRef = GetString(payloadJson, "requestIdRef"),
            RequestStationIdRef = GetString(payloadJson, "requestStationIdRef"),
            ResponderId = GetString(payloadJson, "responderId")            
        };
    }

    private static string ResolveStationId(Packet packet, JsonDocument? payloadJson)
    {
        var stationId = GetString(payloadJson, "stationId");
        if (!string.IsNullOrWhiteSpace(stationId))
        {
            return stationId;
        }

        if (!string.IsNullOrWhiteSpace(packet.SourceMac))
        {
            return packet.SourceMac;
        }

        return "unknown";
    }

    private static JsonDocument? TryParsePayloadJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var trimmed = payload.Trim();
        if (TryParseJson(trimmed, out var directJson))
        {
            return directJson;
        }

        if (TryHexToBytes(trimmed, out var payloadBytes))
        {
            var utf8Text = Encoding.UTF8.GetString(payloadBytes).Trim('\0', ' ', '\t', '\r', '\n');
            if (TryParseJson(utf8Text, out var hexJson))
            {
                return hexJson;
            }
        }

        return null;
    }

    private static bool TryParseJson(string candidate, out JsonDocument? jsonDocument)
    {
        jsonDocument = null;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var first = candidate[0];
        if (first != '{' && first != '[')
        {
            return false;
        }

        try
        {
            jsonDocument = JsonDocument.Parse(candidate);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryHexToBytes(string value, out byte[] bytes)
    {
        var normalized = value
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

        if (normalized.Length < 2 || normalized.Length % 2 != 0)
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        bytes = new byte[normalized.Length / 2];
        for (var i = 0; i < normalized.Length; i += 2)
        {
            if (!byte.TryParse(normalized.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i / 2]))
            {
                bytes = Array.Empty<byte>();
                return false;
            }
        }

        return true;
    }

    private static string GetString(JsonDocument? payloadJson, string key, string fallback = "")
    {
        if (TryGetValue(payloadJson, key, out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? fallback,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => fallback
            };
        }

        return fallback;
    }

    private static double GetDouble(JsonDocument? payloadJson, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryGetValue(payloadJson, key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var num))
            {
                return num;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0d;
    }

    private static int GetInt(JsonDocument? payloadJson, string key, int fallback = 0)
    {
        if (TryGetValue(payloadJson, key, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var num))
            {
                return num;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static int GetFirstInt(JsonDocument? payloadJson, string[] keys, int fallback = 0)
    {
        foreach (var key in keys)
        {
            var value = GetInt(payloadJson, key, int.MinValue);
            if (value != int.MinValue)
            {
                return value;
            }
        }

        return fallback;
    }

    private static string GetFirstString(JsonDocument? payloadJson, string fallback, string[] keys)
    {
        foreach (var key in keys)
        {
            var value = GetString(payloadJson, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return fallback;
    }

    private static int GetSpatemSignalGroup(JsonDocument? payloadJson, int phaseIndex)
    {
        var keys = phaseIndex == 0
            ? new[] { "phase0SignalGroup", "currentPhase" }
            : new[] { $"phase{phaseIndex}SignalGroup", $"currentPhase{phaseIndex}" };

        return GetFirstInt(payloadJson, keys);
    }

    private static string GetSpatemEventState(JsonDocument? payloadJson, int phaseIndex, string fallback = "")
    {
        var keys = phaseIndex == 0
            ? new[] { "phase0EventState", "phaseState" }
            : new[] { $"phase{phaseIndex}EventState", $"phaseState{phaseIndex}" };

        return GetFirstString(payloadJson, fallback, keys);
    }

    private static int GetSpatemConnectionManeuverAssistId(JsonDocument? payloadJson, int phaseIndex, int maneuverIndex)
    {
        var keys = new List<string>
        {
            $"phase{phaseIndex}ConnectionManeuverAssistId{maneuverIndex}"
        };

        if (phaseIndex == 0 && maneuverIndex == 0)
        {
            keys.Add("connectionManeuverAssistId");
        }

        if (phaseIndex == 0 && maneuverIndex == 1)
        {
            keys.Add("connectionManeuverAssistId1");
        }

        if (phaseIndex > 0 && maneuverIndex == 0)
        {
            keys.Add($"connectionManeuverAssistId{phaseIndex}");
        }

        if (phaseIndex > 0 && maneuverIndex == 1)
        {
            keys.Add($"connectionManeuverAssistId{phaseIndex}1");
        }

        return GetFirstInt(payloadJson, keys.ToArray());
    }

    private static string DetermineCamDecodeStatus(double latitude, double longitude, double speed, double heading, int stationType)
    {
        var hasPosition = latitude != 0d && longitude != 0d;
        var hasKinematics = speed != 0d || heading != 0d;
        var hasStationType = stationType != 0;

        if (hasPosition && hasKinematics && hasStationType)
        {
            return "Decoded";
        }

        if (hasPosition || hasKinematics || hasStationType)
        {
            return "Partial";
        }

        return "Failed";
    }

    private static double ScaleCoordinate(double value)
    {
        if (Math.Abs(value) > 180d)
        {
            return value / 10000000d;
        }

        return value;
    }

    private static double ScaleAltitude(double value)
    {
        if (Math.Abs(value) > 10000d)
        {
            return value / 100d;
        }

        return value;
    }
    private static double ScaleHeading(double value)
    {
        if (Math.Abs(value) > 360d)
        {
            return value / 10d;
        }

        return value;
    }

    private static double ScaleAcceleration(double value)
    {
        if (Math.Abs(value) > 30d)
        {
            return value / 10d;
        }

        return value;
    }

    private static double ScaleCurvature(double value)
    {
        if (Math.Abs(value) > 2d)
        {
            return value / 10000d;
        }

        return value;
    }

    private static double ScaleYawRate(double value)
    {
        if (Math.Abs(value) > 100d)
        {
            return value / 100d;
        }

        return value;
    }

    private static double ScaleLateralAcceleration(double value)
    {
        if (Math.Abs(value) > 20d)
        {
            return value / 10d;
        }

        return value;
    }

    private static double ScaleVerticalAcceleration(double value)
    {
        if (Math.Abs(value) > 20d)
        {
            return value / 10d;
        }

        return value;
    }

    private static bool GetBool(JsonDocument? payloadJson, string key, bool fallback)
    {
        if (TryGetValue(payloadJson, key, out var value))
        {
            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static DateTime GetDateTime(JsonDocument? payloadJson, string key, DateTime fallback)
    {
        if (TryGetValue(payloadJson, key, out var value))
        {
            if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
            {
                return dt;
            }
        }

        return fallback;
    }

    private static bool TryGetValue(JsonDocument? payloadJson, string key, out JsonElement value)
    {
        value = default;
        if (payloadJson is null)
        {
            return false;
        }

        var propertyIndex = PropertyIndexCache.GetValue(payloadJson, static doc =>
        {
            var index = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            IndexPayloadProperties(doc.RootElement, index, null);
            return index;
        });

        return propertyIndex.TryGetValue(key, out value);
    }

    private static void IndexPayloadProperties(
        JsonElement element,
        Dictionary<string, JsonElement> index,
        string? pathPrefix)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!index.ContainsKey(property.Name))
                {
                    index[property.Name] = property.Value;
                }

                var qualifiedName = string.IsNullOrEmpty(pathPrefix)
                    ? property.Name
                    : $"{pathPrefix}.{property.Name}";

                if (!index.ContainsKey(qualifiedName))
                {
                    index[qualifiedName] = property.Value;
                }

                IndexPayloadProperties(property.Value, index, qualifiedName);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                IndexPayloadProperties(item, index, pathPrefix);
            }
        }
    }
    private static double CmtoM(double value)
    {
        return value / 100d;
    }

    private static double DmtoM(double value)
    {
        return value / 10d;
    }

    private static double mapemSpeedScaler(double speed)
    {
        if (speed > 0) 
        {
            double scaler = 3.6*3.6;
            return Math.Floor(speed / scaler);
        }

        return speed;
    }

}
