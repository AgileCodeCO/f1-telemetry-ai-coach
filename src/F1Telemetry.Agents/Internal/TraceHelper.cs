using F1Telemetry.Contracts;

namespace F1Telemetry.Agents.Internal;

internal static class TraceHelper
{
    internal static IReadOnlyList<TelemetryFrame>[] SplitSectors(IReadOnlyList<TelemetryFrame> frames)
    {
        if (frames.Count == 0)
        {
            return [Array.Empty<TelemetryFrame>(), Array.Empty<TelemetryFrame>(), Array.Empty<TelemetryFrame>()];
        }

        int third = Math.Max(1, frames.Count / 3);
        return
        [
            frames.Take(third).ToList(),
            frames.Skip(third).Take(third).ToList(),
            frames.Skip(2 * third).ToList(),
        ];
    }
}
