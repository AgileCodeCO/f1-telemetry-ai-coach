namespace F1Telemetry.Contracts;

public interface ILapRepository
{
    Task SaveLapAsync(CompletedLap lap, CancellationToken ct = default);
    Task<IReadOnlyList<CompletedLap>> GetLapsBySessionAsync(SessionId sessionId, CancellationToken ct = default);
    Task<CompletedLap?> GetPersonalBestAsync(CancellationToken ct = default);
}
