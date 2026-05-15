using F1Telemetry.Contracts;
using F1Telemetry.Storage.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace F1Telemetry.App.Services;

internal sealed partial class ReplayHostedService(
    ILapArchive lapArchive,
    IEventBus eventBus,
    IOptions<StorageOptions> storageOptions,
    ILogger<ReplayHostedService> logger) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Replay mode is disabled — skipping replay")]
    private static partial void LogReplayDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replay starting: {SessionCount} session(s) found")]
    private static partial void LogReplayStarting(ILogger logger, int sessionCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replaying session {SessionId}: {LapCount} lap(s)")]
    private static partial void LogReplaySession(ILogger logger, string sessionId, int lapCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replayed lap {LapNumber} for session {SessionId}")]
    private static partial void LogLapReplayed(ILogger logger, int lapNumber, string sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replay complete")]
    private static partial void LogReplayComplete(ILogger logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!storageOptions.Value.ReplayMode)
        {
            LogReplayDisabled(logger);
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        IReadOnlyList<SessionId> sessions = await lapArchive.ListSessionsAsync(stoppingToken);
        LogReplayStarting(logger, sessions.Count);

        foreach (SessionId sessionId in sessions)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            IReadOnlyList<int> lapNumbers = await lapArchive.ListLapNumbersAsync(sessionId, stoppingToken);
            LogReplaySession(logger, sessionId.Value, lapNumbers.Count);

            foreach (int lapNumber in lapNumbers)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                CompletedLap? lap = await lapArchive.ReadAsync(sessionId, lapNumber, stoppingToken);
                if (lap is not null)
                {
                    eventBus.Publish(new LapCompletedEvent(lap));
                    LogLapReplayed(logger, lapNumber, sessionId.Value);
                }

                // Pace the replay so the AI agents aren't flooded
                await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
            }
        }

        LogReplayComplete(logger);
    }
}
