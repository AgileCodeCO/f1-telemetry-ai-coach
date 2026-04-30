using System.Globalization;
using System.Text;
using System.Text.Json;
using F1Telemetry.Agents.Internal;
using F1Telemetry.Contracts;
using Microsoft.Extensions.AI;

namespace F1Telemetry.Agents.Agents;

internal sealed class DeltaAgent(IChatClient chatClient) : ILapAgent
{
    private const string SystemPrompt =
        "You are an F1 lap time analysis assistant. Analyse the provided sector time comparison " +
        "and identify the single biggest opportunity for improvement. " +
        "Respond with only a JSON object in this exact format: " +
        "{\"finding\":\"<description of the opportunity>\",\"estimated_gain_ms\":<integer milliseconds>}";

    public async Task<AgentFinding?> AnalyseAsync(LapAnalysisContext context, CancellationToken ct = default)
    {
        if (context.PersonalBestLap is null)
        {
            return null;
        }

        string userPrompt = BuildPrompt(context);
        ChatResponse chatResponse = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, SystemPrompt), new ChatMessage(ChatRole.User, userPrompt)],
            cancellationToken: ct);
        string response = chatResponse.Text ?? string.Empty;
        return ParseResponse(response);
    }

    internal static string BuildPrompt(LapAnalysisContext context)
    {
        CompletedLap current = context.CurrentLap;
        CompletedLap pb = context.PersonalBestLap!;

        int currentMs = (int)current.LapTime.TotalMilliseconds;
        int pbMs = (int)pb.LapTime.TotalMilliseconds;
        int currentS1 = (int)current.Sector1.TotalMilliseconds;
        int currentS2 = (int)current.Sector2.TotalMilliseconds;
        int currentS3 = (int)current.Sector3.TotalMilliseconds;
        int pbS1 = (int)pb.Sector1.TotalMilliseconds;
        int pbS2 = (int)pb.Sector2.TotalMilliseconds;
        int pbS3 = (int)pb.Sector3.TotalMilliseconds;

        int deltaS1 = currentS1 - pbS1;
        int deltaS2 = currentS2 - pbS2;
        int deltaS3 = currentS3 - pbS3;
        int deltaTotal = currentMs - pbMs;

        var sb = new StringBuilder();
        sb.AppendLine("Lap comparison (current vs personal best):");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Current lap:  {currentMs}ms  (S1: {currentS1}ms  S2: {currentS2}ms  S3: {currentS3}ms)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Personal best:{pbMs}ms  (S1: {pbS1}ms  S2: {pbS2}ms  S3: {pbS3}ms)");
        sb.AppendLine();
        sb.AppendLine("Sector deltas (positive = slower than PB):");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Sector 1: {FormatDelta(deltaS1)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Sector 2: {FormatDelta(deltaS2)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Sector 3: {FormatDelta(deltaS3)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Total:    {FormatDelta(deltaTotal)}");
        sb.AppendLine();
        sb.Append("Identify the biggest single opportunity and estimate the realistic time gain.");

        return sb.ToString();
    }

    private static string FormatDelta(int deltaMs)
    {
        if (deltaMs > 0)
        {
            return "+" + deltaMs.ToString(CultureInfo.InvariantCulture) + "ms";
        }
        else if (deltaMs < 0)
        {
            return deltaMs.ToString(CultureInfo.InvariantCulture) + "ms";
        }
        else
        {
            return "0ms";
        }
    }

    internal static AgentFinding? ParseResponse(string response)
    {
        try
        {
            int start = response.IndexOf('{');
            int end = response.LastIndexOf('}');

            if (start < 0 || end < 0 || end <= start)
            {
                return null;
            }

            string json = response[start..(end + 1)];

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string finding = root.GetProperty("finding").GetString() ?? string.Empty;
            int gainMs = root.GetProperty("estimated_gain_ms").GetInt32();

            return new AgentFinding("DeltaAgent", AnalysisCategory.Delta, finding, gainMs);
        }
#pragma warning disable CA1031 // intentional broad catch for malformed LLM responses
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}
