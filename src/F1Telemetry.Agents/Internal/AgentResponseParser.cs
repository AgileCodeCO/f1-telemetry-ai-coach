using System.Text.Json;
using F1Telemetry.Contracts;

namespace F1Telemetry.Agents.Internal;

internal static class AgentResponseParser
{
    internal static AgentFinding? Parse(string response, string agentName, AnalysisCategory category)
    {
        try
        {
            int start = response.IndexOf('{');
            int end = response.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            string json = response[start..(end + 1)];
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            string finding = root.GetProperty("finding").GetString() ?? string.Empty;
            int gainMs = root.GetProperty("estimated_gain_ms").GetInt32();
            return new AgentFinding(agentName, category, finding, gainMs);
        }
#pragma warning disable CA1031 // intentional broad catch for malformed LLM responses
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}
