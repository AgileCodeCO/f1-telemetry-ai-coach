using F1Telemetry.Storage.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace F1Telemetry.App.HealthChecks;

internal sealed class InfluxDbHealthCheck(InfluxDbWatchdogService watchdog) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        HealthCheckResult result = watchdog.IsHealthy
            ? HealthCheckResult.Healthy("InfluxDB is reachable")
            : HealthCheckResult.Unhealthy("InfluxDB is unreachable");

        return Task.FromResult(result);
    }
}
