namespace F1Telemetry.Contracts;

// Parsed representation of the 30-byte F1 2024 packet header.
public sealed record PacketHeader(
    ushort PacketFormat,
    PacketId PacketId,
    ulong SessionUid,
    float SessionTime,
    uint FrameIdentifier,
    byte PlayerCarIndex)
{
    public static PacketHeader Empty { get; } = new(0, PacketId.Motion, 0, 0, 0, 0);
}
