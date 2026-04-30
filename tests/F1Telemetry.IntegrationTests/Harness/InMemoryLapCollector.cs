using F1Telemetry.Contracts;

namespace F1Telemetry.IntegrationTests.Harness;

internal interface ILapCompletedCollector
{
    IReadOnlyList<CompletedLap> ReceivedLaps { get; }
    void Add(CompletedLap lap);
    Task WaitForCountAsync(int expected, TimeSpan timeout, CancellationToken ct = default);
}

/// <summary>
/// Thread-safe collector for LapCompletedEvents received during a test.
/// </summary>
internal sealed class InMemoryLapCollector : ILapCompletedCollector, IDisposable
{
    private readonly List<CompletedLap> _laps = [];
    private readonly SemaphoreSlim _signal = new(0);

    public IReadOnlyList<CompletedLap> ReceivedLaps
    {
        get
        {
            lock (_laps)
            {
                return [.. _laps];
            }
        }
    }

    public void Add(CompletedLap lap)
    {
        lock (_laps)
        {
            _laps.Add(lap);
        }

        _signal.Release();
    }

    /// <summary>
    /// Waits until <paramref name="expected"/> laps have been collected, or throws on timeout.
    /// </summary>
    public async Task WaitForCountAsync(int expected, TimeSpan timeout, CancellationToken ct = default)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);

        for (int i = 0; i < expected; i++)
        {
            await _signal.WaitAsync(linked.Token);
        }
    }

    public void Dispose() => _signal.Dispose();
}
