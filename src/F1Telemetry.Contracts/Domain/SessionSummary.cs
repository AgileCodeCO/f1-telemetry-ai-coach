namespace F1Telemetry.Contracts;

public sealed record SessionSummary(
    SessionId SessionId,
    string TrackName,
    string SessionType,
    DateTimeOffset StartedAt,
    int LapCount,
    int? BestLapTimeMs);
