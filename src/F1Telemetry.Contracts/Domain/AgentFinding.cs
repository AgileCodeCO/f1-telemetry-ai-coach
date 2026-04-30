namespace F1Telemetry.Contracts;

public sealed record AgentFinding(
    string AgentName,
    AnalysisCategory Category,
    string Finding,
    int EstimatedGainMs);
