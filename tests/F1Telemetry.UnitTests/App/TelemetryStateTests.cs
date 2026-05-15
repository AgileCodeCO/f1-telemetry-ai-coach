using F1Telemetry.App.Dtos;
using F1Telemetry.App.Services;
using F1Telemetry.Contracts;
using FluentAssertions;

namespace F1Telemetry.UnitTests.App;

public sealed class TelemetryStateTests
{
    private static TelemetryFrame MakeFrame() => new(
        SessionId: SessionId.From(1),
        LapNumber: 1,
        SessionTime: 0f,
        SpeedKmh: 200f,
        Throttle: 1f,
        Brake: 0f,
        Gear: 5,
        EngineRpm: 10000,
        Drs: false,
        TyreTempFl: 90f, TyreTempFr: 90f, TyreTempRl: 90f, TyreTempRr: 90f,
        WorldPositionX: 0f, WorldPositionY: 0f, WorldPositionZ: 0f);

    [Fact]
    public void TryAddFrame_FirstFrame_AcceptsIt()
    {
        DateTimeOffset time = DateTimeOffset.UtcNow;
        var state = new TelemetryState(() => time);

        bool accepted = state.TryAddFrame(MakeFrame());

        accepted.Should().BeTrue();
        state.Frames.Should().HaveCount(1);
        state.Latest.Should().NotBeNull();
    }

    [Fact]
    public void TryAddFrame_WithinInterval_DropsFrame()
    {
        DateTimeOffset time = DateTimeOffset.UtcNow;
        var state = new TelemetryState(() => time);

        state.TryAddFrame(MakeFrame());
        bool second = state.TryAddFrame(MakeFrame());

        second.Should().BeFalse();
        state.Frames.Should().HaveCount(1);
    }

    [Fact]
    public void TryAddFrame_AfterInterval_AcceptsFrame()
    {
        DateTimeOffset time = DateTimeOffset.UtcNow;
        var state = new TelemetryState(() => time);

        state.TryAddFrame(MakeFrame());
        time += TimeSpan.FromMilliseconds(101);
        bool second = state.TryAddFrame(MakeFrame());

        second.Should().BeTrue();
        state.Frames.Should().HaveCount(2);
    }

    [Fact]
    public void TryAddFrame_ExceedsMaxWindow_TrimsOldFrames()
    {
        DateTimeOffset time = DateTimeOffset.UtcNow;
        var state = new TelemetryState(() => time);

        for (int i = 0; i < 505; i++)
        {
            time += TimeSpan.FromMilliseconds(200);
            state.TryAddFrame(MakeFrame());
        }

        state.Frames.Should().HaveCount(500);
    }

    [Fact]
    public void TryAddFrame_RaisesFrameDecimatedEvent()
    {
        DateTimeOffset time = DateTimeOffset.UtcNow;
        var state = new TelemetryState(() => time);
        TelemetryFrameDto? received = null;
        state.FrameDecimated += dto => received = dto;

        state.TryAddFrame(MakeFrame());

        received.Should().NotBeNull();
        received!.SpeedKmh.Should().Be(200f);
    }

    [Fact]
    public void OnNewLap_ClearsFrameBuffer()
    {
        DateTimeOffset time = DateTimeOffset.UtcNow;
        var state = new TelemetryState(() => time);

        state.TryAddFrame(MakeFrame());
        state.Frames.Should().HaveCount(1);

        state.OnNewLap();

        state.Frames.Should().BeEmpty();
    }
}
