namespace F1Telemetry.Contracts;

public interface ILapArchive
{
    Task WriteAsync(CompletedLap lap, CancellationToken ct = default);
    Task<CompletedLap?> ReadAsync(SessionId sessionId, int lapNumber, CancellationToken ct = default);
    Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<int>> ListLapNumbersAsync(SessionId sessionId, CancellationToken ct = default);
}
