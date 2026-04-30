namespace F1Telemetry.Storage.Options;

internal sealed class InfluxDbOptions
{
    public string Url { get; set; } = "http://localhost:8086";
    public string Token { get; set; } = string.Empty;
    public string Org { get; set; } = "f1telemetry";
    public string Bucket { get; set; } = "telemetry";
}
