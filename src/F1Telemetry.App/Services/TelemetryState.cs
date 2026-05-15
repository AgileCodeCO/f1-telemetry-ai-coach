using F1Telemetry.App.Dtos;
using F1Telemetry.Contracts;

namespace F1Telemetry.App.Services;

public sealed class TelemetryState
{
    private const int MaxFrames = 500;
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(100);

    private readonly Func<DateTimeOffset> _clock;
    private readonly List<TelemetryFrameDto> _frames = [];
    private readonly object _lock = new();
    private DateTimeOffset _lastAdded = DateTimeOffset.MinValue;

    public TelemetryState() : this(() => DateTimeOffset.UtcNow) { }

    public TelemetryState(Func<DateTimeOffset> clock) => _clock = clock;

    public event Action<TelemetryFrameDto>? FrameDecimated;

    public TelemetryFrameDto? Latest { get; private set; }

    public IReadOnlyList<TelemetryFrameDto> Frames
    {
        get
        {
            lock (_lock) { return [.. _frames]; }
        }
    }

    public bool TryAddFrame(TelemetryFrame frame)
    {
        DateTimeOffset now = _clock();

        lock (_lock)
        {
            if (now - _lastAdded < MinInterval)
            {
                return false;
            }

            _lastAdded = now;
            TelemetryFrameDto dto = TelemetryFrameDto.FromDomain(frame);
            _frames.Add(dto);

            if (_frames.Count > MaxFrames)
            {
                _frames.RemoveAt(0);
            }

            Latest = dto;
        }

        FrameDecimated?.Invoke(Latest!);
        return true;
    }

    public void OnNewLap()
    {
        lock (_lock) { _frames.Clear(); }
    }
}
