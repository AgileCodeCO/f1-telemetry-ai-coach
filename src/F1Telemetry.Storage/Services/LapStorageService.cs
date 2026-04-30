using F1Telemetry.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace F1Telemetry.Storage.Services;

internal sealed partial class LapStorageService(
    IEventBus eventBus,
    IServiceScopeFactory scopeFactory,
    ILapArchive lapArchive,
    ITelemetryRepository telemetryRepository,
    ILogger<LapStorageService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        eventBus.Subscribe<LapCompletedEvent>(OnLapCompleted);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void OnLapCompleted(LapCompletedEvent e)
    {
        _ = PersistLapAsync(e.Lap);
    }

    private async Task PersistLapAsync(CompletedLap lap)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ILapRepository lapRepository = scope.ServiceProvider.GetRequiredService<ILapRepository>();

            await Task.WhenAll(
                lapRepository.SaveLapAsync(lap),
                lapArchive.WriteAsync(lap),
                WriteFramesAsync(lap));

            LogLapPersisted(logger, lap.LapNumber, lap.SessionId);
        }
        catch (Exception ex)
        {
            LogPersistError(logger, ex, lap.LapNumber, lap.SessionId);
        }
    }

    private async Task WriteFramesAsync(CompletedLap lap)
    {
        foreach (TelemetryFrame frame in lap.Frames)
        {
            await telemetryRepository.WriteFrameAsync(frame);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Lap {LapNumber} (session {SessionId}) persisted to all stores")]
    private static partial void LogLapPersisted(ILogger logger, int lapNumber, SessionId sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to persist lap {LapNumber} (session {SessionId})")]
    private static partial void LogPersistError(ILogger logger, Exception ex, int lapNumber, SessionId sessionId);
}
