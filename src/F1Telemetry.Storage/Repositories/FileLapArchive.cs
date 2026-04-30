using System.Text.Json;
using F1Telemetry.Contracts;
using F1Telemetry.Storage.Options;
using Microsoft.Extensions.Options;

namespace F1Telemetry.Storage.Repositories;

internal sealed class FileLapArchive(IOptions<StorageOptions> options) : ILapArchive
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task WriteAsync(CompletedLap lap, CancellationToken ct = default)
    {
        string dir = GetSessionDir(lap.SessionId);
        Directory.CreateDirectory(dir);
        string path = GetLapPath(lap.SessionId, lap.LapNumber);
        string json = JsonSerializer.Serialize(lap, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    public async Task<CompletedLap?> ReadAsync(SessionId sessionId, int lapNumber, CancellationToken ct = default)
    {
        string path = GetLapPath(sessionId, lapNumber);
        if (!File.Exists(path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<CompletedLap>(json);
    }

    private string GetSessionDir(SessionId sessionId)
    {
        string root = options.Value.ArchivePath
            .Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        return Path.Combine(root, sessionId.Value);
    }

    private string GetLapPath(SessionId sessionId, int lapNumber) =>
        Path.Combine(GetSessionDir(sessionId), $"lap_{lapNumber:D2}.json");
}
