namespace F1Telemetry.Agents.Options;

public sealed class LlmOptions
{
    public string Provider { get; set; } = "ollama";
    public string Model { get; set; } = "llama3.2";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiKey { get; set; } = string.Empty;
}
