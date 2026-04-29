namespace F1Telemetry.Contracts;

public sealed record PacketCarStatusData : IParsedPacket
{
    public PacketHeader Header { get; init; } = PacketHeader.Empty;
    public TyreCompound VisualTyreCompound { get; init; }
    public byte TyresAgeLaps { get; init; }
    public float FuelInTank { get; init; }
}
