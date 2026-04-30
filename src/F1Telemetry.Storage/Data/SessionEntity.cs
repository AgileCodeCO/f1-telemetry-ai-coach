namespace F1Telemetry.Storage.Data;

internal sealed class SessionEntity
{
    public string Id { get; set; } = string.Empty;
    public string TrackName { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public string StartedAt { get; set; } = string.Empty;
    public string? TyreCompound { get; set; }
    public List<LapEntity> Laps { get; set; } = [];
}
