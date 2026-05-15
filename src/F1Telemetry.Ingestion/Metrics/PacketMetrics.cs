using System.Diagnostics.Metrics;

namespace F1Telemetry.Ingestion.Metrics;

public sealed class PacketMetrics : IDisposable
{
    private readonly Meter _meter;
    private long _totalReceived;
    private long _totalDropped;

    public PacketMetrics()
    {
        _meter = new Meter("F1Telemetry.Ingestion");
        _meter.CreateObservableCounter("f1.udp.packets_received", () => _totalReceived, "packets");
        _meter.CreateObservableCounter("f1.udp.packets_dropped", () => _totalDropped, "packets");
    }

    public long TotalReceived => Interlocked.Read(ref _totalReceived);
    public long TotalDropped => Interlocked.Read(ref _totalDropped);

    public double DropRate =>
        _totalReceived == 0 ? 0.0 : (double)_totalDropped / _totalReceived;

    public void RecordReceived() => Interlocked.Increment(ref _totalReceived);
    public void RecordDropped() => Interlocked.Increment(ref _totalDropped);

    public void Dispose() => _meter.Dispose();
}
