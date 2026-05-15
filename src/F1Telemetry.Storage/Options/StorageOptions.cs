namespace F1Telemetry.Storage.Options;

public sealed class StorageOptions
{
    public string ArchivePath { get; set; } = "~/f1telemetry/sessions";
    public bool ReplayMode { get; set; }
}
