namespace F1Telemetry.Storage.Data;

internal sealed class LapFeedbackEntity
{
    public int Id { get; set; }
    public int LapId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Finding { get; set; } = string.Empty;
    public int? EstimatedGainMs { get; set; }
    public string GeneratedAt { get; set; } = string.Empty;
    public LapEntity Lap { get; set; } = null!;
}
