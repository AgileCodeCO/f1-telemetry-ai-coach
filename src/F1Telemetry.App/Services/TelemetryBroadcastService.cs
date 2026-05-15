using F1Telemetry.App.Dtos;
using F1Telemetry.App.Hubs;
using F1Telemetry.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace F1Telemetry.App.Services;

internal sealed class TelemetryBroadcastService(
    TelemetryState telemetryState,
    IHubContext<TelemetryHub> hubContext,
    IEventBus eventBus) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        eventBus.Subscribe<TelemetryFramePublishedEvent>(OnTelemetryFrame);
        eventBus.Subscribe<LapCompletedEvent>(OnLapCompleted);
        eventBus.Subscribe<CoachingReportReadyEvent>(OnCoachingReportReady);
        telemetryState.FrameDecimated += OnFrameDecimated;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        telemetryState.FrameDecimated -= OnFrameDecimated;
        return Task.CompletedTask;
    }

    private void OnTelemetryFrame(TelemetryFramePublishedEvent e) =>
        telemetryState.TryAddFrame(e.Frame);

    private void OnFrameDecimated(TelemetryFrameDto dto) =>
        _ = hubContext.Clients.All.SendAsync("ReceiveTelemetryFrame", dto);

    private void OnLapCompleted(LapCompletedEvent e)
    {
        telemetryState.OnNewLap();
        _ = hubContext.Clients.All.SendAsync("LapCompleted", LapSummaryDto.FromDomain(e.Lap));
    }

    private void OnCoachingReportReady(CoachingReportReadyEvent e) =>
        _ = hubContext.Clients.All.SendAsync("CoachingReportReady", CoachingReportDto.FromDomain(e.Report));
}
