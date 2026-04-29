namespace F1Telemetry.Contracts;

public sealed record CompletedLap(
    SessionId SessionId,
    int LapNumber,
    TimeSpan LapTime,
    TimeSpan Sector1,
    TimeSpan Sector2,
    TimeSpan Sector3,
    bool IsValid,
    IReadOnlyList<TelemetryFrame> Frames);
