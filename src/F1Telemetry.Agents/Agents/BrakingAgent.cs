using System.Globalization;
using System.Text;
using F1Telemetry.Agents.Internal;
using F1Telemetry.Contracts;
using Microsoft.Extensions.AI;

namespace F1Telemetry.Agents.Agents;

internal sealed class BrakingAgent(IChatClient chatClient) : ILapAgent
{
    private const string AgentName = "BrakingAgent";
    private const string SystemPrompt =
        "You are an F1 braking coach. Analyse the sector-by-sector braking data and identify " +
        "the single biggest opportunity to improve brake application. " +
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
        return AgentResponseParser.Parse(response, AgentName, AnalysisCategory.Braking);
    }

    private static string BuildPrompt(LapAnalysisContext context)
    {
        IReadOnlyList<TelemetryFrame>[] curSectors = TraceHelper.SplitSectors(context.CurrentLapTrace);
        IReadOnlyList<TelemetryFrame>[] pbSectors = TraceHelper.SplitSectors(context.PersonalBestTrace);

        StringBuilder sb = new();
        sb.AppendLine("Braking comparison (current lap vs personal best, per sector):");
        sb.AppendLine();

        for (int i = 0; i < 3; i++)
        {
            (float maxCur, float avgCur, float pctCur) = BrakingStats(curSectors[i]);
            (float maxPb, float avgPb, float pctPb) = BrakingStats(pbSectors[i]);
            sb.AppendLine(CultureInfo.InvariantCulture, $"Sector {i + 1}:");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  Current — peak: {maxCur:F2}  avg: {avgCur:F2}  braking: {pctCur:F0}% of sector");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  PB      — peak: {maxPb:F2}  avg: {avgPb:F2}  braking: {pctPb:F0}% of sector");
        }

        sb.Append("Identify where braking deviates from the PB and estimate the potential time gain.");
        return sb.ToString();
    }

    private static (float MaxBrake, float AvgBrake, float BrakePct) BrakingStats(
        IReadOnlyList<TelemetryFrame> frames)
    {
        if (frames.Count == 0)
        {
            return (0f, 0f, 0f);
        }

        float max = 0f;
        float sum = 0f;
        int brakeCount = 0;

        foreach (TelemetryFrame f in frames)
        {
            if (f.Brake > max)
            {
                max = f.Brake;
            }

            sum += f.Brake;
            if (f.Brake > 0.1f)
            {
                brakeCount++;
            }
        }

        return (max, sum / frames.Count, 100f * brakeCount / frames.Count);
    }
}
