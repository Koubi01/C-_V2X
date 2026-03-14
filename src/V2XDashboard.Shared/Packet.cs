using System;

namespace V2XDashboard.Shared;

public class Packet
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string SourceMac { get; set; } = string.Empty;
    public string DestinationMac { get; set; } = string.Empty;
    public string PacketType { get; set; } = string.Empty; // 802.11p, ITS-5G, etc.
    public int Length { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string SourceIp { get; set; } = string.Empty;
    public string DestinationIp { get; set; } = string.Empty;
    public int SourcePort { get; set; }
    public int DestinationPort { get; set; }
    public string Payload { get; set; } = string.Empty; // Hex or base64 encoded
    public string PcapFileName { get; set; } = string.Empty;
}
