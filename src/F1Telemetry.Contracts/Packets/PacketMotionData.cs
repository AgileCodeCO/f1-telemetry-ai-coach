namespace F1Telemetry.Contracts;

public sealed record PacketMotionData : IParsedPacket
{
    public PacketHeader Header { get; init; } = PacketHeader.Empty;
    public float WorldPositionX { get; init; }
    public float WorldPositionY { get; init; }
    public float WorldPositionZ { get; init; }
    public float WorldVelocityX { get; init; }
    public float WorldVelocityY { get; init; }
    public float WorldVelocityZ { get; init; }
    public float GForceLateral { get; init; }
    public float GForceLongitudinal { get; init; }
    public float GForceVertical { get; init; }
}
