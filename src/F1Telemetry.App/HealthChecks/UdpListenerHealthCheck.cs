using F1Telemetry.Ingestion.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace F1Telemetry.App.HealthChecks;

internal sealed class UdpListenerHealthCheck(PacketMetrics metrics) : IHealthCheck
{
    private const double MaxAllowedDropRate = 0.01; // 1%

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        double dropRate = metrics.DropRate;
        long received = metrics.TotalReceived;
        long dropped = metrics.TotalDropped;

        HealthCheckResult result = dropRate > MaxAllowedDropRate
            ? HealthCheckResult.Degraded(
                $"UDP packet drop rate {dropRate:P1} exceeds 1% threshold " +
                $"({dropped}/{received} packets dropped)",
                data: new Dictionary<string, object>
                {
                    ["packets_received"] = received,
                    ["packets_dropped"] = dropped,
                    ["drop_rate"] = dropRate
                })
            : HealthCheckResult.Healthy(
                $"UDP listener healthy — {received} packets received, {dropped} dropped",
                data: new Dictionary<string, object>
                {
                    ["packets_received"] = received,
                    ["packets_dropped"] = dropped,
                    ["drop_rate"] = dropRate
                });

        return Task.FromResult(result);
    }
}
