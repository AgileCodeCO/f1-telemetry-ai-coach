using F1Telemetry.Agents.Internal;
using F1Telemetry.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace F1Telemetry.Agents.Services;

internal sealed partial class AgentOrchestrator(
    IEnumerable<ILapAgent> agents,
    IServiceScopeFactory scopeFactory,
    ITelemetryRepository telemetryRepository,
    IEventBus eventBus,
    ILogger<AgentOrchestrator> logger) : IHostedService
{
    private readonly IReadOnlyList<ILapAgent> _agents = agents.ToList().AsReadOnly();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        eventBus.Subscribe<LapCompletedEvent>(OnLapCompleted);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void OnLapCompleted(LapCompletedEvent e)
    {
        _ = RunAnalysisAsync(e.Lap);
    }

    internal async Task RunAnalysisAsync(CompletedLap lap, CancellationToken ct = default)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ILapRepository lapRepository = scope.ServiceProvider.GetRequiredService<ILapRepository>();

            CompletedLap? pb = await lapRepository.GetPersonalBestAsync(ct);

            IReadOnlyList<TelemetryFrame> currentTrace =
                await telemetryRepository.GetLapTraceAsync(lap.SessionId, lap.LapNumber, ct);

            IReadOnlyList<TelemetryFrame> pbTrace = pb is not null
                ? await telemetryRepository.GetLapTraceAsync(pb.SessionId, pb.LapNumber, ct)
                : [];

            LapAnalysisContext context = new(lap, pb, currentTrace, pbTrace);

            AgentFinding?[] rawFindings = await Task.WhenAll(
                _agents.Select(agent => agent.AnalyseAsync(context, ct)));

            var findings = rawFindings
                .OfType<AgentFinding>()
                .OrderByDescending(f => f.EstimatedGainMs)
                .ToList();

            LapCoachingReport report = new(lap, findings.AsReadOnly());

            LogCoachingReport(logger, lap.LapNumber, findings.Count);

            foreach (AgentFinding finding in findings)
            {
                LogFinding(logger, finding.AgentName, finding.EstimatedGainMs, finding.Finding);
                await lapRepository.SaveFeedbackAsync(lap.SessionId, lap.LapNumber, finding, ct);
            }

            eventBus.Publish(new CoachingReportReadyEvent(report));
        }
#pragma warning disable CA1031 // intentional broad catch to keep hosted service alive
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogAnalysisError(logger, lap.LapNumber, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Lap {LapNumber} coaching report: {FindingCount} finding(s)")]
    private static partial void LogCoachingReport(ILogger logger, int lapNumber, int findingCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "  [{AgentName}] +{GainMs}ms — {Finding}")]
    private static partial void LogFinding(ILogger logger, string agentName, int gainMs, string finding);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error running analysis for lap {LapNumber}")]
    private static partial void LogAnalysisError(ILogger logger, int lapNumber, Exception ex);
}
