using System.Globalization;
using System.Text;
using F1Telemetry.Agents.Internal;
using F1Telemetry.Contracts;
using Microsoft.Extensions.AI;

namespace F1Telemetry.Agents.Agents;

internal sealed class TyreAgent(IChatClient chatClient) : ILapAgent
{
    private const string AgentName = "TyreAgent";
    private const string SystemPrompt =
        "You are an F1 tyre coach. Analyse the tyre temperature data to identify overheating or " +
        "asymmetric load patterns that are costing lap time. " +
        "Respond with only: {\"finding\":\"<coaching observation>\",\"estimated_gain_ms\":<integer>}";

    public async Task<AgentFinding?> AnalyseAsync(LapAnalysisContext context, CancellationToken ct = default)
    {
        if (context.CurrentLapTrace.Count == 0)
        {
            return null;
        }

        if (context.CurrentLapTrace.All(f => f.TyreTempFl == 0f && f.TyreTempFr == 0f))
        {
            return null;
        }

        string userPrompt = BuildPrompt(context);
        ChatResponse chatResponse = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, SystemPrompt), new ChatMessage(ChatRole.User, userPrompt)],
            cancellationToken: ct);
        string response = chatResponse.Text ?? string.Empty;
        return AgentResponseParser.Parse(response, AgentName, AnalysisCategory.Tyre);
    }

    private static string BuildPrompt(LapAnalysisContext context)
    {
        IReadOnlyList<TelemetryFrame>[] curSectors = TraceHelper.SplitSectors(context.CurrentLapTrace);

        StringBuilder sb = new();
        sb.AppendLine("Tyre temperature data — current lap:");
        sb.AppendLine();

        for (int i = 0; i < 3; i++)
        {
            (float fl, float fr, float rl, float rr) = AvgTemps(curSectors[i]);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"Sector {i + 1} avg — FL: {fl:F0}°C  FR: {fr:F0}°C  RL: {rl:F0}°C  RR: {rr:F0}°C  " +
                $"(front Δ: {MathF.Abs(fl - fr):F0}°C  rear Δ: {MathF.Abs(rl - rr):F0}°C)");
        }

        if (context.PersonalBestTrace.Count > 0)
        {
            IReadOnlyList<TelemetryFrame>[] pbSectors = TraceHelper.SplitSectors(context.PersonalBestTrace);
            sb.AppendLine();
            sb.AppendLine("Personal best lap for reference:");
            for (int i = 0; i < 3; i++)
            {
                (float fl, float fr, float rl, float rr) = AvgTemps(pbSectors[i]);
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"Sector {i + 1} avg — FL: {fl:F0}°C  FR: {fr:F0}°C  RL: {rl:F0}°C  RR: {rr:F0}°C");
            }
        }

        sb.Append("Identify tyre temperature concerns and driving style adjustments that would help.");
        return sb.ToString();
    }

    private static (float Fl, float Fr, float Rl, float Rr) AvgTemps(IReadOnlyList<TelemetryFrame> frames)
    {
        if (frames.Count == 0)
        {
            return (0f, 0f, 0f, 0f);
        }

        float fl = 0f, fr = 0f, rl = 0f, rr = 0f;

        foreach (TelemetryFrame f in frames)
        {
            fl += f.TyreTempFl;
            fr += f.TyreTempFr;
            rl += f.TyreTempRl;
            rr += f.TyreTempRr;
        }

        int n = frames.Count;
        return (fl / n, fr / n, rl / n, rr / n);
    }
}
