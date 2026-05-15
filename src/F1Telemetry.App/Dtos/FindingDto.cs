using F1Telemetry.Contracts;

namespace F1Telemetry.App.Dtos;

public sealed record FindingDto(
    string AgentName,
    string Category,
    string Finding,
    int EstimatedGainMs)
{
    public static FindingDto FromDomain(AgentFinding f) =>
        new(f.AgentName, f.Category.ToString(), f.Finding, f.EstimatedGainMs);
}
