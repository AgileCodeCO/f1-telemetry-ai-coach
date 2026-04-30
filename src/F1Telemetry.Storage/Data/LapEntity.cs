namespace F1Telemetry.Storage.Data;

internal sealed class LapEntity
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int LapNumber { get; set; }
    public int? LapTimeMs { get; set; }
    public int? Sector1Ms { get; set; }
    public int? Sector2Ms { get; set; }
    public int? Sector3Ms { get; set; }
    public bool IsPersonalBest { get; set; }
    public bool IsValid { get; set; } = true;
    public float? MaxSpeedKmh { get; set; }
    public float? AvgThrottle { get; set; }
    public SessionEntity Session { get; set; } = null!;
    public List<LapFeedbackEntity> Feedback { get; set; } = [];
}
