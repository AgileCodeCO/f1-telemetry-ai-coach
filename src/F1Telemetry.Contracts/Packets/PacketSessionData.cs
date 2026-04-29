namespace F1Telemetry.Contracts;

public sealed record PacketSessionData : IParsedPacket
{
    public PacketHeader Header { get; init; } = PacketHeader.Empty;
    public SessionType SessionType { get; init; }
    public sbyte TrackId { get; init; }
    public byte TotalLaps { get; init; }
    public ushort TrackLength { get; init; }
}
