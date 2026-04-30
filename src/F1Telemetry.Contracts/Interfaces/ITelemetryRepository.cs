namespace F1Telemetry.Contracts;

public interface ITelemetryRepository
{
    Task WriteFrameAsync(TelemetryFrame frame, CancellationToken ct = default);
    Task<IReadOnlyList<TelemetryFrame>> GetLapTraceAsync(SessionId sessionId, int lapNumber, CancellationToken ct = default);
}
