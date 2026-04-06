namespace V2XDashboard.Server.Services.PcapReader.TsharkWrapper;

public sealed class PacketTypeClassifier : IPacketTypeClassifier
{
    public string ClassifyFromMessageId(string messageId)
    {
        return messageId switch
        {
            "1" => "DENM",
            "2" => "CAM",
            "3" => "POI",
            "4" => "SPATEM",
            "5" => "MAPEM",
            "6" => "IVIM",
            "7" => "SREM",
            "8" => "SSEM",
            "9" => "SREM",
            "10" => "SSEM",
            _ => "ITS-Unknown"
        };
    }
}
