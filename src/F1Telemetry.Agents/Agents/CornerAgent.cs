using System.Globalization;
using System.Text;
using F1Telemetry.Agents.Internal;
using F1Telemetry.Contracts;
using Microsoft.Extensions.AI;

namespace F1Telemetry.Agents.Agents;

internal sealed class CornerAgent(IChatClient chatClient) : ILapAgent
{
    private const string AgentName = "CornerAgent";
    private const string SystemPrompt =
        "You are an F1 corner-exit coach. Analyse the sector-by-sector minimum corner speed and " +
        "throttle application data to identify the single biggest improvement opportunity. " +
        "Respond with only: {\"finding\":\"<coaching observation>\",\"estimated_gain_ms\":<integer>}";

    public async Task<AgentFinding?> AnalyseAsync(LapAnalysisContext context, CancellationToken ct = default)
    {
        if (context.PersonalBestLap is null
            || context.CurrentLapTrace.Count == 0
            || context.PersonalBestTrace.Count == 0)
        {
            return null;
        }

        string userPrompt = BuildPrompt(context);
        ChatResponse chatResponse = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, SystemPrompt), new ChatMessage(ChatRole.User, userPrompt)],
            cancellationToken: ct);
        string response = chatResponse.Text ?? string.Empty;
        return AgentResponseParser.Parse(response, AgentName, AnalysisCategory.Corner);
    }

    private static string BuildPrompt(LapAnalysisContext context)
    {
        IReadOnlyList<TelemetryFrame>[] curSectors = TraceHelper.SplitSectors(context.CurrentLapTrace);
        IReadOnlyList<TelemetryFrame>[] pbSectors = TraceHelper.SplitSectors(context.PersonalBestTrace);

        StringBuilder sb = new();
        sb.AppendLine("Corner speed and throttle comparison (current vs personal best):");
        sb.AppendLine();

        for (int i = 0; i < 3; i++)
        {
            (float minSpeedCur, float avgSpeedCur, float throttlePctCur) = CornerStats(curSectors[i]);
            (float minSpeedPb, float avgSpeedPb, float throttlePctPb) = CornerStats(pbSectors[i]);
            sb.AppendLine(CultureInfo.InvariantCulture, $"Sector {i + 1}:");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  Current — min speed: {minSpeedCur:F0} km/h  avg: {avgSpeedCur:F0} km/h  on-throttle: {throttlePctCur:F0}%");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  PB      — min speed: {minSpeedPb:F0} km/h  avg: {avgSpeedPb:F0} km/h  on-throttle: {throttlePctPb:F0}%");
        }

        sb.Append("Identify where corner speed or throttle pickup can be improved and estimate the gain.");
        return sb.ToString();
    }

    private static (float MinSpeed, float AvgSpeed, float ThrottlePct) CornerStats(
        IReadOnlyList<TelemetryFrame> frames)
    {
        if (frames.Count == 0)
        {
            return (0f, 0f, 0f);
        }

        float min = float.MaxValue;
        float sum = 0f;
        int throttleCount = 0;

        foreach (TelemetryFrame f in frames)
        {
            if (f.SpeedKmh < min)
            {
                min = f.SpeedKmh;
            }

            sum += f.SpeedKmh;
            if (f.Throttle > 0.1f)
            {
                throttleCount++;
            }
        }

        return (min, sum / frames.Count, 100f * throttleCount / frames.Count);
    }
}
