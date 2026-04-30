namespace F1Telemetry.Contracts;

public sealed record LapCoachingReport(
    CompletedLap Lap,
    IReadOnlyList<AgentFinding> Findings);
