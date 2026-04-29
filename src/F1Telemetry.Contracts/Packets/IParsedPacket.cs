namespace F1Telemetry.Contracts;

public interface IParsedPacket
{
    PacketHeader Header { get; }
}
