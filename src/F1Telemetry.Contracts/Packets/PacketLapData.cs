namespace F1Telemetry.Contracts;

public sealed record PacketLapData : IParsedPacket
{
    public PacketHeader Header { get; init; } = PacketHeader.Empty;
    public uint LastLapTimeMs { get; init; }
    public uint CurrentLapTimeMs { get; init; }
    public uint Sector1TimeMs { get; init; }
    public uint Sector2TimeMs { get; init; }
    public byte CurrentLapNum { get; init; }
    public byte PitStatus { get; init; }
    public bool CurrentLapInvalid { get; init; }
}
