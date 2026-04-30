using System.Globalization;
using System.Text;
using F1Telemetry.Agents.Internal;
using F1Telemetry.Contracts;
using Microsoft.Extensions.AI;

namespace F1Telemetry.Agents.Agents;

internal sealed class RacingLineAgent(IChatClient chatClient) : ILapAgent
{
    private const string AgentName = "RacingLineAgent";
    private const string SystemPrompt =
        "You are an F1 racing line coach. Analyse the driven line (XZ position spread) vs the personal " +
        "best to identify entry or exit deviations. Identify the single biggest opportunity. " +
        "Respond with only: {\"finding\":\"<coaching observation>\",\"estimated_gain_ms\":<integer>}";

    public async Task<AgentFinding?> AnalyseAsync(LapAnalysisContext context, CancellationToken ct = default)
    {
        if (context.PersonalBestLap is null
            || context.CurrentLapTrace.Count == 0
            || context.PersonalBestTrace.Count == 0)
        {
            return null;
        }

        if (context.CurrentLapTrace.All(f => f.WorldPositionX == 0f && f.WorldPositionZ == 0f))
        {
            return null;
        }

        string userPrompt = BuildPrompt(context);
        ChatResponse chatResponse = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, SystemPrompt), new ChatMessage(ChatRole.User, userPrompt)],
            cancellationToken: ct);
        string response = chatResponse.Text ?? string.Empty;
        return AgentResponseParser.Parse(response, AgentName, AnalysisCategory.RacingLine);
    }

    private static string BuildPrompt(LapAnalysisContext context)
    {
        IReadOnlyList<TelemetryFrame>[] curSectors = TraceHelper.SplitSectors(context.CurrentLapTrace);
        IReadOnlyList<TelemetryFrame>[] pbSectors = TraceHelper.SplitSectors(context.PersonalBestTrace);

        StringBuilder sb = new();
        sb.AppendLine("Racing line comparison — position spread (XZ) vs personal best:");
        sb.AppendLine();

        for (int i = 0; i < 3; i++)
        {
            (float curX, float curZ) = PositionSpread(curSectors[i]);
            (float pbX, float pbZ) = PositionSpread(pbSectors[i]);
            float curWidth = MathF.Sqrt(curX * curX + curZ * curZ);
            float pbWidth = MathF.Sqrt(pbX * pbX + pbZ * pbZ);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"Sector {i + 1}: line width {curWidth:F1}m vs PB {pbWidth:F1}m  " +
                $"(X spread {curX:F1}m vs {pbX:F1}m, Z spread {curZ:F1}m vs {pbZ:F1}m)");
        }

        sb.AppendLine();
        sb.Append("Identify where the line deviates most from the PB and how to correct it.");
        return sb.ToString();
    }

    private static (float XRange, float ZRange) PositionSpread(IReadOnlyList<TelemetryFrame> frames)
    {
        if (frames.Count == 0)
        {
            return (0f, 0f);
        }

        float xMin = float.MaxValue, xMax = float.MinValue;
        float zMin = float.MaxValue, zMax = float.MinValue;

        foreach (TelemetryFrame f in frames)
        {
            if (f.WorldPositionX < xMin) { xMin = f.WorldPositionX; }
            if (f.WorldPositionX > xMax) { xMax = f.WorldPositionX; }
            if (f.WorldPositionZ < zMin) { zMin = f.WorldPositionZ; }
            if (f.WorldPositionZ > zMax) { zMax = f.WorldPositionZ; }
        }

        return (xMax - xMin, zMax - zMin);
    }
}
