namespace F1Telemetry.Contracts;

public sealed record PacketCarDamageData : IParsedPacket
{
    public PacketHeader Header { get; init; } = PacketHeader.Empty;
    // Tyre wear per wheel — indices RL=0, RR=1, FL=2, FR=3 in wire format
    public float TyreWearFl { get; init; }
    public float TyreWearFr { get; init; }
    public float TyreWearRl { get; init; }
    public float TyreWearRr { get; init; }
}
