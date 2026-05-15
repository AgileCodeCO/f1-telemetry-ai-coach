using InfluxDB.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace F1Telemetry.Storage.Services;

public sealed partial class InfluxDbWatchdogService(
    IInfluxDBClient influxClient,
    ILogger<InfluxDbWatchdogService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    public bool IsHealthy { get; private set; } = true;

    [LoggerMessage(Level = LogLevel.Information, Message = "InfluxDB connection restored")]
    private static partial void LogRestored(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "InfluxDB is unreachable: {Message}")]
    private static partial void LogUnreachable(ILogger logger, string message);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
                bool ok = await influxClient.PingAsync();
                if (!ok)
                {
                    throw new InvalidOperationException("Ping returned false");
                }

                if (!IsHealthy)
                {
                    IsHealthy = true;
                    LogRestored(logger);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                IsHealthy = false;
                LogUnreachable(logger, ex.Message);
            }
        }
    }
}
