using F1Telemetry.Contracts;
using F1Telemetry.Storage.Options;
using F1Telemetry.Storage.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace F1Telemetry.UnitTests.Storage;

public sealed class FileLapArchiveTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileLapArchive _archive;

    public FileLapArchiveTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"f1test_{Guid.NewGuid():N}");
        _archive = new FileLapArchive(Options.Create(new StorageOptions { ArchivePath = _tempDir }));
    }

    [Fact]
    public async Task WriteAsync_CreatesJsonFileAtExpectedPath()
    {
        CompletedLap lap = BuildLap(lapNumber: 3);

        await _archive.WriteAsync(lap);

        string expectedPath = Path.Combine(_tempDir, lap.SessionId.Value, "lap_03.json");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task WriteAsync_ThenReadAsync_RoundTripsLapData()
    {
        CompletedLap lap = BuildLap(lapNumber: 1, lapTimeMs: 84_500);

        await _archive.WriteAsync(lap);
        CompletedLap? result = await _archive.ReadAsync(lap.SessionId, lap.LapNumber);

        result.Should().NotBeNull();
        result!.LapNumber.Should().Be(1);
        result.LapTime.TotalMilliseconds.Should().BeApproximately(84_500, precision: 1);
        result.SessionId.Should().Be(lap.SessionId);
    }

    [Fact]
    public async Task ReadAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        CompletedLap? result = await _archive.ReadAsync(SessionId.From(0xDEAD000000000001UL), lapNumber: 99);
        result.Should().BeNull();
    }

    [Fact]
    public async Task WriteAsync_TildeInPath_ExpandsToHomeDirectory()
    {
        string homeRelativePath = Path.Combine("~", $"f1test_{Guid.NewGuid():N}");
        FileLapArchive archiveWithTilde = new(Options.Create(new StorageOptions { ArchivePath = homeRelativePath }));

        CompletedLap lap = BuildLap(lapNumber: 1);
        await archiveWithTilde.WriteAsync(lap);

        string expandedDir = homeRelativePath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        string path = Path.Combine(expandedDir, lap.SessionId.Value, "lap_01.json");
        File.Exists(path).Should().BeTrue();

        Directory.Delete(expandedDir, recursive: true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static CompletedLap BuildLap(int lapNumber, uint lapTimeMs = 90_000) =>
        new(
            SessionId: SessionId.From(0xDEADBEEF00000001UL),
            LapNumber: lapNumber,
            LapTime: TimeSpan.FromMilliseconds(lapTimeMs),
            Sector1: TimeSpan.FromMilliseconds(lapTimeMs / 3),
            Sector2: TimeSpan.FromMilliseconds(lapTimeMs / 3),
            Sector3: TimeSpan.FromMilliseconds(lapTimeMs / 3),
            IsValid: true,
            Frames: []);
}
