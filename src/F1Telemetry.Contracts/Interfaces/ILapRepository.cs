namespace F1Telemetry.Contracts;

public interface ILapRepository
{
    Task SaveLapAsync(CompletedLap lap, CancellationToken ct = default);
    Task<IReadOnlyList<CompletedLap>> GetLapsBySessionAsync(SessionId sessionId, CancellationToken ct = default);
    Task<CompletedLap?> GetPersonalBestAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SessionSummary>> GetAllSessionsAsync(CancellationToken ct = default);
    Task SaveFeedbackAsync(SessionId sessionId, int lapNumber, AgentFinding finding, CancellationToken ct = default);
    Task<IReadOnlyList<AgentFinding>> GetFeedbackAsync(SessionId sessionId, int lapNumber, CancellationToken ct = default);
}
