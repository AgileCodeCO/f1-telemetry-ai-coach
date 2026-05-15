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

    public Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken ct = default)
    {
        string root = ExpandArchivePath();
        if (!Directory.Exists(root))
        {
            return Task.FromResult<IReadOnlyList<SessionId>>([]);
        }

        IReadOnlyList<SessionId> sessions = Directory.GetDirectories(root)
            .Select(d => new SessionId(Path.GetFileName(d)))
            .ToList()
            .AsReadOnly();

        return Task.FromResult(sessions);
    }

    public Task<IReadOnlyList<int>> ListLapNumbersAsync(SessionId sessionId, CancellationToken ct = default)
    {
        string dir = GetSessionDir(sessionId);
        if (!Directory.Exists(dir))
        {
            return Task.FromResult<IReadOnlyList<int>>([]);
        }

        IReadOnlyList<int> laps = Directory.GetFiles(dir, "lap_*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(n => n.StartsWith("lap_", StringComparison.Ordinal))
            .Select(n => int.TryParse(n["lap_".Length..], out int num) ? num : -1)
            .Where(n => n >= 0)
            .OrderBy(n => n)
            .ToList()
            .AsReadOnly();

        return Task.FromResult(laps);
    }

    private string GetSessionDir(SessionId sessionId) =>
        Path.Combine(ExpandArchivePath(), sessionId.Value);

    private string ExpandArchivePath() =>
        options.Value.ArchivePath
            .Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private string GetLapPath(SessionId sessionId, int lapNumber) =>
        Path.Combine(GetSessionDir(sessionId), $"lap_{lapNumber:D2}.json");
}
