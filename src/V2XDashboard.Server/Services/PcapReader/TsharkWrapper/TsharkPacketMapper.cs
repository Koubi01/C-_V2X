using System.Globalization;
using System.Text.Json;
using V2XDashboard.Server.Infrastructure.Time;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.TsharkWrapper;

public sealed class TsharkPacketMapper : ITsharkPacketMapper
{
    private readonly IClock _clock;
    private readonly IPacketTypeClassifier _packetTypeClassifier;

    public TsharkPacketMapper(IClock clock, IPacketTypeClassifier packetTypeClassifier)
    {
        _clock = clock;
        _packetTypeClassifier = packetTypeClassifier;
    }

    public List<Packet> Map(string jsonOutput, string pcapFileName)
    {
        var packets = new List<Packet>();

        if (string.IsNullOrWhiteSpace(jsonOutput))
            return packets;

        try
        {
            using var document = JsonDocument.Parse(jsonOutput);
            var root = document.RootElement;

            foreach (var packetElement in root.EnumerateArray())
            {
                var packet = new Packet
                {
                    PcapFileName = pcapFileName
                };

                var layers = packetElement.GetProperty("_source").GetProperty("layers");

                if (layers.TryGetProperty("frame", out var frameLayer))
                {
                    if (frameLayer.TryGetProperty("frame.time_epoch", out var timeElement))
                    {
                        packet.Timestamp = ParseTimestamp(timeElement);
                    }
                    else
                    {
                        packet.Timestamp = _clock.UtcNow;
                    }

                    if (frameLayer.TryGetProperty("frame.len", out var lengthElement))
                    {
                        if (int.TryParse(GetStringValue(lengthElement), NumberStyles.Integer, CultureInfo.InvariantCulture, out var length))
                        {
                            packet.Length = length;
                        }
                    }
                }
                else
                {
                    packet.Timestamp = _clock.UtcNow;
                }

                if (layers.TryGetProperty("eth", out var ethLayer))
                {
                    if (ethLayer.TryGetProperty("eth.dst", out var destinationMac))
                    {
                        packet.DestinationMac = GetStringValue(destinationMac);
                    }

                    if (ethLayer.TryGetProperty("eth.src", out var sourceMac))
                    {
                        packet.SourceMac = GetStringValue(sourceMac);
                    }
                }

                if (layers.TryGetProperty("wlan", out var wlanLayer))
                {
                    if (wlanLayer.TryGetProperty("wlan.sa", out var sourceMacElement))
                    {
                        packet.SourceMac = GetStringValue(sourceMacElement);
                    }

                    if (wlanLayer.TryGetProperty("wlan.da", out var destMacElement))
                    {
                        packet.DestinationMac = GetStringValue(destMacElement);
                    }
                }

                if (layers.TryGetProperty("btpb", out var btpLayer))
                {
                    if (btpLayer.TryGetProperty("btpb.dstport", out var btpDstPortElement))
                    {
                        if (int.TryParse(GetStringValue(btpDstPortElement), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
                        {
                            packet.DestinationPort = port;
                        }
                    }
                }

                packet.Payload = CreateNormalizedPayload(packet, layers);

                packets.Add(packet);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse tshark JSON output: {ex.Message}", ex);
        }

        return packets;
    }

    private static string GetStringValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Array when element.GetArrayLength() > 0 => GetStringValue(element[0]),
            JsonValueKind.Number => element.GetRawText(),
            _ => string.Empty
        };
    }

    private DateTime ParseTimestamp(JsonElement timeElement)
    {
        var rawValue = GetStringValue(timeElement);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return _clock.UtcNow;
        }

        if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedIso))
        {
            return parsedIso;
        }

        if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var epochSeconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(epochSeconds * 1000)).UtcDateTime;
        }

        return _clock.UtcNow;
    }

    private string CreateNormalizedPayload(Packet packet, JsonElement layers)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var secureSigned = HasLayerOrField(
            layers,
            "ieee1609dot2",
            "signature",
            "signeddata",
            "signed_data");
        var secureEncrypted = HasLayerOrField(
            layers,
            "encrypteddata",
            "encrypted_data",
            "recipientinfo",
            "aesccm");

        var securityProtocol = ResolveSecurityProtocol(layers, secureSigned, secureEncrypted);
        var signerId = ExtractFirstByKeyContains(layers, "signer", "signerid", "hashid8", "requestorid");
        var certificateId = ExtractFirstByKeyContains(layers, "certificate", "certid", "cert_id", "cert");

        packet.IsSecureSigned = secureSigned;
        packet.IsSecureEncrypted = secureEncrypted;
        packet.SecurityProtocol = securityProtocol;
        packet.SignerId = signerId;
        packet.CertificateId = certificateId;

        payload["isSecureSigned"] = secureSigned;
        payload["isSecureEncrypted"] = secureEncrypted;
        payload["securityProtocol"] = securityProtocol;
        if (!string.IsNullOrWhiteSpace(signerId))
        {
            payload["signerId"] = signerId;
        }

        if (!string.IsNullOrWhiteSpace(certificateId))
        {
            payload["certificateId"] = certificateId;
        }

        if (TryGetNestedString(layers, out var messageId, "its", "its.ItsPduHeader_element", "its.messageId"))
        {
            payload["messageId"] = messageId;
            packet.PacketType = _packetTypeClassifier.ClassifyFromMessageId(messageId);
        }

        if (TryGetNestedString(layers, out var stationId, "its", "its.ItsPduHeader_element", "its.stationId"))
        {
            payload["stationId"] = stationId;
        }

        if(packet.PacketType == "CAM")
        {
            if (TryGetNestedString(layers, out var latitude, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.basicContainer_element", "its.referencePosition_element", "its.latitude"))
            {
                payload["latitude"] = latitude;
            }

            if (TryGetNestedString(layers, out var longitude, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.basicContainer_element", "its.referencePosition_element", "its.longitude"))
            {
                payload["longitude"] = longitude;
            }

            if (TryGetNestedString(layers, out var altitude, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.basicContainer_element", "its.referencePosition_element", "its.altitude_element", "its.altitudeValue"))
            {
                payload["altitude"] = altitude;
            }
            
            if (TryGetNestedString(layers, out var speed, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.highFrequencyContainer_tree", "cam.basicVehicleContainerHighFrequency_element", "cam.speed_element", "its.speedValue"))
            {
                payload["speed"] = speed;
            }

            if (TryGetNestedString(layers, out var heading, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.highFrequencyContainer_tree", "cam.basicVehicleContainerHighFrequency_element", "cam.heading_element", "its.headingValue"))
            {
                payload["heading"] = heading;
            }

            if (TryGetNestedString(layers, out var stationType, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.basicContainer_element", "its.stationType"))
            {
                payload["stationType"] = stationType;
            }

            if (TryGetNestedString(layers, out var vehicleRole, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.lowFrequencyContainer_tree", "cam.basicVehicleContainerLowFrequency_element", "cam.vehicleRole"))
            {
                payload["vehicleRole"] = vehicleRole;
            }        

            if (TryGetNestedString(layers, out var acceleration, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.highFrequencyContainer_tree", "cam.basicVehicleContainerHighFrequency_element", "cam.longitudinalAcceleration_element", "its.value"))
            {
                payload["acceleration"] = acceleration;
            }

            if (TryGetNestedString(layers, out var curvature, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.highFrequencyContainer_tree", "cam.basicVehicleContainerHighFrequency_element", "cam.curvature_element", "its.curvatureValue"))
            {
                payload["curvature"] = curvature;
            }

            if (TryGetNestedString(layers, out var yawRate, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.highFrequencyContainer_tree", "cam.basicVehicleContainerHighFrequency_element", "cam.yawRate_element", "its.yawRateValue"))
            {
                payload["yawRate"] = yawRate;
            }

            if (TryGetNestedString(layers, out var lateralAcceleration, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.highFrequencyContainer_tree", "cam.basicVehicleContainerHighFrequency_element", "cam.lateralAcceleration_element", "its.value"))
            {
                payload["lateralAcceleration"] = lateralAcceleration;
            }

            if (TryGetNestedString(layers, out var verticalAcceleration, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.highFrequencyContainer_tree", "cam.basicVehicleContainerHighFrequency_element", "cam.verticalAcceleration_element", "its.value"))
            {
                payload["verticalAcceleration"] = verticalAcceleration;
            }

            if (TryGetNestedString(layers, out var vehicleLength, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.highFrequencyContainer_tree", "cam.basicVehicleContainerHighFrequency_element", "cam.vehicleLength_element", "its.vehicleLengthValue"))
            {
                payload["vehicleLength"] = vehicleLength;
            }

            if (TryGetNestedString(layers, out var vehicleWidth, "its", "cam.CamPayload_element", "cam.camParameters_element", "cam.highFrequencyContainer_tree", "cam.basicVehicleContainerHighFrequency_element", "cam.vehicleWidth"))
            {
                payload["vehicleWidth"] = vehicleWidth;
            }
        }
        if(packet.PacketType == "DENM")
        {
            if (TryGetNestedString(layers, out var denmLatitude, "its", "denm.DenmPayload_element", "denm.management_element", "denm.eventPosition_element", "its.latitude"))
            {
                payload["latitude"] = denmLatitude;
            }

            if (TryGetNestedString(layers, out var denmLongitude, "its", "denm.DenmPayload_element", "denm.management_element", "denm.eventPosition_element", "its.longitude"))
            {
                payload["longitude"] = denmLongitude;
            }

            if (TryGetNestedString(layers, out var denmAltitude, "its", "denm.DenmPayload_element", "denm.management_element", "denm.eventPosition_element", "its.altitude_element", "its.altitudeValue"))
            {
                payload["altitude"] = denmAltitude;
            }

            if (TryGetNestedString(layers, out var causeCode, "its", "denm.DenmPayload_element", "denm.situation_element", "denm.eventType_element", "its.ccAndScc"))
            {
                payload["causeCode"] = causeCode;
            }

            if (TryGetNestedString(layers, out var detectionTime, "its", "denm.DenmPayload_element", "denm.management_element", "denm.detectionTime"))
            {
                payload["detectionTime"] = detectionTime;
            }

            if (TryGetNestedString(layers, out var referenceTime, "its", "denm.DenmPayload_element", "denm.management_element", "denm.referenceTime"))
            {
                payload["referenceTime"] = referenceTime;
            }

            if (TryGetNestedString(layers, out var validityDuration, "its", "denm.DenmPayload_element", "denm.management_element", "denm.validityDuration"))
            {
                payload["validityDuration"] = validityDuration;
            }

            if (TryGetNestedString(layers, out var relevanceStationType, "its", "denm.DenmPayload_element", "denm.management_element", "denm.stationType"))
            {
                payload["relevanceStationType"] = relevanceStationType;
            }

            if (TryGetNestedString(layers, out var awarenessTrafficDirection, "its", "denm.DenmPayload_element", "denm.management_element", "denm.awarenessTrafficDirection"))
            {
                payload["awarenessTrafficDirection"] = awarenessTrafficDirection;
            }

            if (TryGetNestedString(layers, out var denmStationType, "its", "denm.DenmPayload_element", "denm.management_element", "denm.actionId_element","its.originatingStationId"))
            {
                payload["denmOriginalStationId"] = denmStationType;
            }
        }
        if(packet.PacketType == "MAPEM")
        {
            if (TryGetNestedStringAny(layers, out var intersectionId,
                    ["its", "dsrc.MapData_element", "dsrc.mdIntersections_tree", "Item 0", "dsrc.IntersectionGeometry_element", "dsrc.igId_element", "dsrc.irId"]))
            {
                payload["intersectionId"] = intersectionId;
            }

            if (TryGetNestedStringAny(layers, out var intersectionName,
                    ["its", "dsrc.MapData_element", "dsrc.mdIntersections_tree", "Item 0", "dsrc.IntersectionGeometry_element", "dsrc.name"]))
            {
                payload["intersectionName"] = intersectionName;
            }

            if (TryGetNestedStringAny(layers, out var mapLatitude,
                    ["its", "dsrc.MapData_element", "dsrc.mdIntersections_tree", "Item 0", "dsrc.IntersectionGeometry_element", "dsrc.refPoint_element", "dsrc.lat"]))
            {
                payload["latitude"] = mapLatitude;
            }

            if (TryGetNestedStringAny(layers, out var mapLongitude,
                    ["its", "dsrc.MapData_element", "dsrc.mdIntersections_tree", "Item 0", "dsrc.IntersectionGeometry_element", "dsrc.refPoint_element", "dsrc.long"]))
            {
                payload["longitude"] = mapLongitude;
            }

            if (TryGetNestedStringAny(layers, out var laneCount,
                ["its", "dsrc.MapData_element", "dsrc.mdIntersections_tree", "Item 0", "dsrc.IntersectionGeometry_element", "dsrc.laneSet"]))
            {
                payload["laneCount"] = laneCount;
            }

            if (TryGetNestedStringAny(layers, out var roadWidth,
                    ["its", "dsrc.MapData_element", "dsrc.mdIntersections_tree", "Item 0", "dsrc.IntersectionGeometry_element", "dsrc.laneWidth"]))
            {
                payload["roadWidth"] = roadWidth;
            }

            if (TryGetNestedStringAny(layers, out var speedLimit,
                    ["its", "dsrc.MapData_element", "dsrc.mdIntersections_tree", "Item 0", "dsrc.IntersectionGeometry_element", "dsrc.speedLimits_tree", "Item 0", "dsrc.RegulatorySpeedLimit_element", "dsrc.rslSpeed"]))
            {
                payload["speedLimit"] = speedLimit;
            }

            if (TryGetNestedStringAny(layers, out var mapVersion,
                    ["its", "dsrc.MapData_element", "dsrc.msgIssueRevision"]))
            {
                payload["mapVersion"] = mapVersion;
            }
            // MAPEM: Extract publisherId (RSU that published this map)
            if (TryGetNestedString(layers, out var mapemPublisherId, "its", "its.ItsPduHeader_element", "its.stationId"))
            {
                // Only set publisherId for MAPEM messages (detect via message type or presence of MapData)
                if (layers.TryGetProperty("its", out var itsCheckLayer) && 
                    itsCheckLayer.TryGetProperty("dsrc.MapData_element", out _))
                {
                    payload["publisherId"] = mapemPublisherId;
                }
            }
        }
        if(packet.PacketType == "SPATEM")
        {
            if (TryGetNestedStringAny(layers, out var spatIntersectionId,
                    ["its", "dsrc.SPAT_element", "dsrc.spatIntersections_tree", "Item 0", "dsrc.IntersectionState_element", "dsrc.isId_element", "dsrc.irId"]))
            {
                payload["intersectionId"] = spatIntersectionId;
            }

            // Parse up to six movement states, each with up to two maneuver assists.
            for (var movementIndex = 0; movementIndex < 6; movementIndex++)
            {
                if (TryGetNestedStringAny(layers, out var signalGroup,
                        BuildSpatMovementPath(movementIndex, "dsrc.signalGroup")))
                {
                    if (movementIndex == 0)
                    {
                        payload["currentPhase"] = signalGroup;
                    }

                    payload[$"phase{movementIndex}SignalGroup"] = signalGroup;
                }

                if (TryGetNestedStringAny(layers, out var eventState,
                        BuildSpatMovementPath(movementIndex, "dsrc.state_time_speed_tree", "Item 0", "dsrc.MovementEvent_element", "dsrc.eventState")))
                {
                    if (movementIndex == 0)
                    {
                        payload["phaseState"] = eventState;
                    }

                    payload[$"phase{movementIndex}EventState"] = eventState;
                }

                for (var maneuverIndex = 0; maneuverIndex < 2; maneuverIndex++)
                {
                    if (!TryGetNestedStringAny(layers, out var connectionManeuverAssistId,
                            BuildSpatMovementPath(movementIndex,
                                "dsrc.maneuverAssistList_tree",
                                $"Item {maneuverIndex}",
                                "dsrc.ConnectionManeuverAssist_element",
                                "dsrc.connectionID")))
                    {
                        continue;
                    }

                    if (movementIndex == 0 && maneuverIndex == 0)
                    {
                        payload["connectionManeuverAssistId"] = connectionManeuverAssistId;
                    }

                    payload[$"phase{movementIndex}ConnectionManeuverAssistId{maneuverIndex}"] = connectionManeuverAssistId;
                }
            }
            // SPATEM: Extract publisherId (RSU that published this SPAT)
            if (TryGetNestedString(layers, out var spatPublisherId, "its", "its.ItsPduHeader_element", "its.stationId"))
            {
                // Only set publisherId for SPATEM messages (detect via presence of SPAT_element)
                if (layers.TryGetProperty("its", out var itsCheckLayer2) && 
                    itsCheckLayer2.TryGetProperty("dsrc.SPAT_element", out _))
                {
                    payload["publisherId"] = spatPublisherId;
                }
            }
        }
        if(packet.PacketType == "SREM")
        {
            if (TryGetNestedStringAny(layers, out var name, ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.name" ]))
            {
                payload["requestorName"] = name;
            }

            if (TryGetNestedStringAny(layers, out var lat, ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.rdPosition_element", "dsrc.rpvPosition_element","dsrc.lat"]))
            {
                payload["latitude"] = lat;
            }

            if (TryGetNestedStringAny(layers, out var lon, ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.rdPosition_element", "dsrc.rpvPosition_element","dsrc.long"]))
            {
                payload["longitude"] = lon;
            }

            if (TryGetNestedStringAny(layers, out var requestedPhase,
                    ["its", "dsrc.SignalRequestMessage_element", "dsrc.requests_tree", "Item 0", "dsrc.SignalRequestPackage_element", "dsrc.srpRequest_element", "dsrc.requestID"]))
            {
                payload["requestedPhase"] = requestedPhase;
            }
        
            if (TryGetNestedString(layers, out var vehicleType, ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.rdType_element", "dsrc.role"]))
            {
                payload["vehicleType"] = vehicleType;
            }        
            
            if (TryGetNestedStringAny(layers, out var requestReason,
                ["its", "dsrc.SignalRequestMessage_element", "dsrc.requests_tree", "Item 0", "dsrc.SignalRequestPackage_element", "dsrc.srpRequest_element", "dsrc.requestType"]))
            {
                payload["requestReason"] = requestReason;
            }

            if (TryGetNestedStringAny(layers, out var requestId,
                ["its", "dsrc.SignalRequestMessage_element", "dsrc.requests_tree", "Item 0", "dsrc.SignalRequestPackage_element", "dsrc.srpRequest_element", "dsrc.srId_element", "dsrc.irId"]))
            {
                payload["requestId"] = requestId;
            }

            if (TryGetNestedStringAny(layers, out var requestorId,
                    ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.rdId_tree", "dsrc.stationID"]))
            {
                payload["requestorId"] = requestorId;
            }

            if (TryGetNestedStringAny(layers, out var requiredAccuracy,
                    ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.rdPosition_element", "dsrc.accuracy"]))
            {
                payload["requiredAccuracy"] = requiredAccuracy;
            }

            if (TryGetNestedStringAny(layers, out var inBoundLane,
                ["its", "dsrc.SignalRequestMessage_element", "dsrc.requests_tree", "Item 0", "dsrc.SignalRequestPackage_element", "dsrc.srpRequest_element", "dsrc.inBoundLane_tree", "dsrc.approach" ]))
            {
                payload["inBoundLane"] = inBoundLane;
            }
            if (TryGetNestedStringAny(layers, out var outBoundLane,
                ["its", "dsrc.SignalRequestMessage_element", "dsrc.requests_tree", "Item 0", "dsrc.SignalRequestPackage_element", "dsrc.srpRequest_element", "dsrc.outBoundLane_tree", "dsrc.approach" ]))
            {
                payload["outBoundLane"] = outBoundLane;
            }        
            
            if (TryGetNestedStringAny(layers, out var headingSRem, ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.rdPosition_element", "dsrc.rpvHeading"]))
            {
                payload["heading"] = headingSRem;
            }
            if (TryGetNestedStringAny(layers, out var tasSpeed, ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.rdPosition_element", "dsrc.rpvSpeed_element","dsrc.tasSpeed"]))
            {
                payload["tasSpeed"] = tasSpeed;
            }
            if (TryGetNestedStringAny(layers, out var transmissionPower, ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.rdPosition_element", "dsrc.rpvSpeed_element","dsrc.transmisson"]))
            {
                payload["transmissionPower"] = transmissionPower;
            }
            
            if (TryGetNestedStringAny(layers, out var routeNames, ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.routeName" ]))
            {
                payload["routeNames"] = routeNames;
            }
            if (TryGetNestedStringAny(layers, out var transitSchedule, ["its", "dsrc.SignalRequestMessage_element", "dsrc.requestor_element", "dsrc.transitSchedule"]))
            {
                payload["transitSchedule"] = transitSchedule;
            }
        }
        if(packet.PacketType == "SSEM")
        {
            if (TryGetNestedStringAny(layers, out var ssemIntersectionId,
                    ["its", "dsrc.SignalStatusMessage_element", "dsrc.signalStatusMessage.status_tree", "Item 0", "dsrc.SignalStatus_element", "dsrc.ssId_element", "dsrc.irId"]))
            {
                payload["intersectionId"] = ssemIntersectionId;
            }

            if (TryGetNestedStringAny(layers, out var statusCode,
                    ["its", "dsrc.SignalStatusMessage_element", "dsrc.signalStatusMessage.status_tree", "Item 0", "dsrc.SignalStatus_element", "dsrc.sigStatus_tree", "Item 0", "dsrc.SignalStatusPackage_element", "dsrc.signalStatusPackage.status"]))
            {
                payload["statusCode"] = statusCode;
            }

            if (TryGetNestedStringAny(layers, out var grantedDuration,
                    ["its", "dsrc.SignalStatusMessage_element", "dsrc.signalStatusMessage.status_tree", "Item 0", "dsrc.SignalStatus_element", "dsrc.sigStatus_tree", "Item 0", "dsrc.SignalStatusPackage_element", "dsrc.duration"]))
            {
                payload["grantedDuration"] = grantedDuration;
            }

            // SSEM: Extract requestIdRef (reference to original SREM requestID)
            if (TryGetNestedStringAny(layers, out var requestIdRef,
                    ["its", "dsrc.SignalStatusMessage_element", "dsrc.signalStatusMessage.status_tree", "Item 0", "dsrc.SignalStatus_element", "dsrc.sigStatus_tree", "Item 0", "dsrc.SignalStatusPackage_element", "dsrc.requester_element", "dsrc.sriRequest"]))
            {
                payload["requestIdRef"] = requestIdRef;
            }

            if (TryGetNestedStringAny(layers, out var requestStationIdRef,
                    ["its", "dsrc.SignalStatusMessage_element", "dsrc.signalStatusMessage.status_tree", "Item 0", "dsrc.SignalStatus_element", "dsrc.sigStatus_tree", "Item 0", "dsrc.SignalStatusPackage_element", "dsrc.requester_element", "dsrc.sriId_tree", "dsrc.stationID"]))
            {
                payload["requestStationIdRef"] = requestStationIdRef;
            }

            // SSEM: Extract responderId (RSU that responded to the request)
            if (TryGetNestedStringAny(layers, out var responderId,
                    ["its", "dsrc.SignalStatusMessage_element", "dsrc.signalStatusMessage.status_tree", "Item 0", "dsrc.SignalStatus_element", "dsrc.ssId_element", "dsrc.irId"]))
            {
                payload["responderId"] = responderId;
            }
        }

        return payload.Count == 0 ? string.Empty : JsonSerializer.Serialize(payload);
    }
    private static string[] BuildSpatMovementPath(int movementIndex, params string[] suffix)
    {
        var prefix = new[]
        {
            "its",
            "dsrc.SPAT_element",
            "dsrc.spatIntersections_tree",
            "Item 0",
            "dsrc.IntersectionState_element",
            "dsrc.states_tree",
            $"Item {movementIndex}",
            "dsrc.MovementState_element"
        };

        return prefix.Concat(suffix).ToArray();
    }

    private static bool TryGetNestedString(JsonElement root, out string value, params string[] path)
    {
        value = string.Empty;
        if (!TryGetNestedProperty(root, out var element, path))
        {
            return false;
        }

        value = GetStringValue(element);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetNestedStringAny(JsonElement root, out string value, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (TryGetNestedString(root, out value, path))
            {
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetNestedProperty(JsonElement root, out JsonElement value, params string[] path)
    {
        value = default;
        var current = root;

        foreach (var key in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(key, out current))
            {
                return false;
            }
        }

        value = current;
        return true;
    }

    private static string ResolveSecurityProtocol(JsonElement layers, bool secureSigned, bool secureEncrypted)
    {
        if (layers.TryGetProperty("ieee1609dot2", out _))
        {
            return "IEEE1609.2";
        }

        if (secureSigned || secureEncrypted)
        {
            return "ITS-SEC";
        }

        return string.Empty;
    }

    private static bool HasLayerOrField(JsonElement root, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (TryFindFirstByKeyContains(root, token, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static string ExtractFirstByKeyContains(JsonElement root, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (TryFindFirstByKeyContains(root, token, out var value))
            {
                var text = GetStringValue(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }

    private static bool TryFindFirstByKeyContains(JsonElement element, string token, out JsonElement found)
    {
        found = default;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    found = property.Value;
                    return true;
                }

                if (TryFindFirstByKeyContains(property.Value, token, out found))
                {
                    return true;
                }
            }

            return false;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindFirstByKeyContains(item, token, out found))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
