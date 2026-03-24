namespace V2XDashboard.Server.Services.PcapReader.TsharkWrapper;

public interface IPacketTypeClassifier
{
    string ClassifyFromMessageId(string messageId);
}
