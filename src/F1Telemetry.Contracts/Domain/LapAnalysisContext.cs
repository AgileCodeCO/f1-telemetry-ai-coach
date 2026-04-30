namespace F1Telemetry.Contracts;

public sealed record LapAnalysisContext(
    CompletedLap CurrentLap,
    CompletedLap? PersonalBestLap,
    IReadOnlyList<TelemetryFrame> CurrentLapTrace,
    IReadOnlyList<TelemetryFrame> PersonalBestTrace);
