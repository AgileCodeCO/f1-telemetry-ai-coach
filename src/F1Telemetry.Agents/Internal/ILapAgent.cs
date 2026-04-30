using F1Telemetry.Contracts;

namespace F1Telemetry.Agents.Internal;

internal interface ILapAgent
{
    Task<AgentFinding?> AnalyseAsync(LapAnalysisContext context, CancellationToken ct = default);
}
