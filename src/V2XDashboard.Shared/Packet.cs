using System;

namespace V2XDashboard.Shared;

public class Packet
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string SourceMac { get; set; } = string.Empty;
    public string DestinationMac { get; set; } = string.Empty;
    public string PacketType { get; set; } = string.Empty;
    public int Length { get; set; }
    public int SourcePort { get; set; }
    public int DestinationPort { get; set; }
    public bool IsSecureSigned { get; set; }
    public bool IsSecureEncrypted { get; set; }
    public string SecurityProtocol { get; set; } = string.Empty;
    public string SignerId { get; set; } = string.Empty;
    public string CertificateId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string PcapFileName { get; set; } = string.Empty;
}
