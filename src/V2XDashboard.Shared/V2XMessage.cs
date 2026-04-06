using System;

namespace V2XDashboard.Shared;

// Base class for all V2X messages
public abstract class V2XMessage
{
    public int Id { get; set; }
    public int PacketId { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public DateTime GenerationTime { get; set; }
    public string StationId { get; set; } = string.Empty;
    public bool IsSecureSigned { get; set; }
    public bool IsSecureEncrypted { get; set; }
    public string SecurityProtocol { get; set; } = string.Empty;
    public string SignerId { get; set; } = string.Empty;
    public string CertificateId { get; set; } = string.Empty;
}

// CAM - Cooperative Awareness Message (Vehicle position, speed, acceleration, etc.)
public class CAM : V2XMessage
{
    public CAM()
    {
        MessageType = "CAM";
    }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double Speed { get; set; }
    public double Heading { get; set; }
    public int StationType { get; set; }
    public string VehicleRole { get; set; } = string.Empty;
    public double Acceleration { get; set; }
    public double Curvature { get; set; }
    public double YawRate { get; set; }
    public double LateralAcceleration { get; set; }
    public double VerticalAcceleration { get; set; }
    public string DecodeStatus { get; set; } = "Failed";
    public double VehicleLength { get; set; }
    public double VehicleWidth { get; set; }
}

// DENM - Decentralized Environmental Notification Message (Safety-related events)
public class DENM : V2XMessage
{
    public DENM()
    {
        MessageType = "DENM";
    }


    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; } 
    public string CauseCode { get; set; } = string.Empty;    
    public DateTime DetectionTime { get; set; }
    public DateTime ReferenceTime { get; set; }
    public int RelevanceTrafficDirection { get; set; }
    public bool ValidityDuration { get; set; }
    public int StationType { get; set; }
    public int AwarenessTrafficDirection { get; set; }
    public int OriginalStationType { get; set; }
}

// MAPEM (MAP) - Map Message (Road network information)
public class MAPEM : V2XMessage
{
    public MAPEM()
    {
        MessageType = "MAPEM";
    }

    public int IntersectionId { get; set; }
    public string IntersectionName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int LaneCount { get; set; }
    public double RoadWidth { get; set; }
    public string SpeedLimit { get; set; } = string.Empty;
    public string MapVersion { get; set; } = string.Empty;
    public string? PublisherId { get; set; } 
}

// SPATEM (SPaT) - Signal Phase and Timing Message
public class SPATEM : V2XMessage
{
    public SPATEM()
    {
        MessageType = "SPATEM";
    }

    public int IntersectionId { get; set; }
    public string IntersectionName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public int CurrentPhase { get; set; }
    public string PhaseState { get; set; } = string.Empty; 
    public int ConnectionManeuverAssistId { get; set; }

    // Up to 6 phase entries can be extracted from SPATEM.
    public int Phase0SignalGroup { get; set; }
    public int Phase1SignalGroup { get; set; }
    public int Phase2SignalGroup { get; set; }
    public int Phase3SignalGroup { get; set; }
    public int Phase4SignalGroup { get; set; }
    public int Phase5SignalGroup { get; set; }

    public string Phase0EventState { get; set; } = string.Empty;
    public string Phase1EventState { get; set; } = string.Empty;
    public string Phase2EventState { get; set; } = string.Empty;
    public string Phase3EventState { get; set; } = string.Empty;
    public string Phase4EventState { get; set; } = string.Empty;
    public string Phase5EventState { get; set; } = string.Empty;

    public int Phase0ConnectionManeuverAssistId0 { get; set; }
    public int Phase0ConnectionManeuverAssistId1 { get; set; }
    public int Phase1ConnectionManeuverAssistId0 { get; set; }
    public int Phase1ConnectionManeuverAssistId1 { get; set; }
    public int Phase2ConnectionManeuverAssistId0 { get; set; }
    public int Phase2ConnectionManeuverAssistId1 { get; set; }
    public int Phase3ConnectionManeuverAssistId0 { get; set; }
    public int Phase3ConnectionManeuverAssistId1 { get; set; }
    public int Phase4ConnectionManeuverAssistId0 { get; set; }
    public int Phase4ConnectionManeuverAssistId1 { get; set; }
    public int Phase5ConnectionManeuverAssistId0 { get; set; }
    public int Phase5ConnectionManeuverAssistId1 { get; set; }

    public string? PublisherId { get; set; } 
}

// SREM - Signal Request Extension Message (Vehicle requests control adjustment)
public class SREM : V2XMessage
{
    public SREM()
    {
        MessageType = "SREM";
    }

    public string IntersectionName { get; set; } = string.Empty;
    public int? IntersectionId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RequestedPhase { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public string RequestReason { get; set; } = string.Empty;
    public string? RequestId { get; set; } 
    public string? RequestorId { get; set; } 
    public string? RequiredAccuracy { get; set; } 
    public int InBoundLaneId { get; set; }
    public int OutBoundLaneId { get; set; }
    public double Heading { get; set; }
    public double Speed { get; set; }
    public int TransmissionPower { get; set; }
    public string RouteNames { get; set; } = string.Empty; 
    public string TransitSchedule { get; set; } = string.Empty; 
    public string RequestorName { get; set; } = string.Empty; 
}

// SSEM - Signal Status Extension Message (Confirmation/status response)
public class SSEM : V2XMessage
{
    public SSEM()
    {
        MessageType = "SSEM";
    }

    public string IntersectionName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }        
    public int IntersectionId { get; set; }    
    public string StatusCode { get; set; } = string.Empty; 
    public int GrantedDuration { get; set; }
    public string? RequestIdRef { get; set; } 
    public string? RequestStationIdRef { get; set; }  
    public string? ResponderId { get; set; } 
}