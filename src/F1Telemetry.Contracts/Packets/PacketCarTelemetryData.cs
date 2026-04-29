namespace F1Telemetry.Contracts;

public sealed record PacketCarTelemetryData : IParsedPacket
{
    public PacketHeader Header { get; init; } = PacketHeader.Empty;
    public float SpeedKmh { get; init; }
    public float Throttle { get; init; }
    public float Brake { get; init; }
    public int Gear { get; init; }
    public int EngineRpm { get; init; }
    public bool Drs { get; init; }
    // Tyre surface temperatures — indices RL=0, RR=1, FL=2, FR=3 in wire format
    public float TyreTempFl { get; init; }
    public float TyreTempFr { get; init; }
    public float TyreTempRl { get; init; }
    public float TyreTempRr { get; init; }
}
