using System.Threading.Channels;
using F1Telemetry.Contracts;
using F1Telemetry.Ingestion.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace F1Telemetry.UnitTests.Ingestion;

public class SessionManagerTests
{
    private static SessionManager CreateManager(IEventBus eventBus)
    {
        var channel = Channel.CreateUnbounded<RawPacket>();
        var parser = Substitute.For<IPacketParser>();
        return new SessionManager(channel.Reader, parser, eventBus, NullLogger<SessionManager>.Instance);
    }

    [Theory]
    [InlineData(0, 0, false)]  // session start, both zero — no event
    [InlineData(1, 1, false)]  // same lap number — no boundary
    [InlineData(1, 2, true)]   // lap 1 → 2 — first lap completed
    [InlineData(5, 6, true)]   // lap 5 → 6 — mid-session boundary
    public void ProcessLapData_DetectsLapBoundary(int previous, int current, bool shouldFire)
    {
        var eventBus = Substitute.For<IEventBus>();
        var manager = CreateManager(eventBus);

        manager.ProcessLapData(new PacketLapData { CurrentLapNum = (byte)previous });
        manager.ProcessLapData(new PacketLapData { CurrentLapNum = (byte)current, LastLapTimeMs = 85_000 });

        if (shouldFire)
        {
            eventBus.Received(1).Publish(Arg.Any<LapCompletedEvent>());
        }
        else
        {
            eventBus.DidNotReceive().Publish(Arg.Any<LapCompletedEvent>());
        }
    }

    [Fact]
    public void ProcessLapData_WhenLapCompletes_PublishesCorrectLapNumber()
    {
        var eventBus = Substitute.For<IEventBus>();
        var manager = CreateManager(eventBus);
        LapCompletedEvent? captured = null;
        eventBus.When(b => b.Publish(Arg.Any<LapCompletedEvent>()))
                .Do(ci => captured = ci.Arg<LapCompletedEvent>());

        manager.ProcessLapData(new PacketLapData { CurrentLapNum = 4 });
        manager.ProcessLapData(new PacketLapData { CurrentLapNum = 5, LastLapTimeMs = 84_200 });

        captured.Should().NotBeNull();
        captured!.Lap.LapNumber.Should().Be(4);
        captured.Lap.LapTime.Should().Be(TimeSpan.FromMilliseconds(84_200));
    }

    [Fact]
    public void ProcessLapData_WhenLapInvalid_SetsIsValidFalse()
    {
        var eventBus = Substitute.For<IEventBus>();
        var manager = CreateManager(eventBus);
        LapCompletedEvent? captured = null;
        eventBus.When(b => b.Publish(Arg.Any<LapCompletedEvent>()))
                .Do(ci => captured = ci.Arg<LapCompletedEvent>());

        manager.ProcessLapData(new PacketLapData { CurrentLapNum = 2 });
        manager.ProcessLapData(new PacketLapData
        {
            CurrentLapNum = 3,
            LastLapTimeMs = 83_000,
            CurrentLapInvalid = true
        });

        captured.Should().NotBeNull();
        captured!.Lap.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ProcessLapData_NewSessionDetected_ResetsLapCounter()
    {
        var eventBus = Substitute.For<IEventBus>();
        var manager = CreateManager(eventBus);

        // Establish lap 3 in session A
        manager.ProcessLapData(new PacketLapData
        {
            Header = new PacketHeader(2024, PacketId.LapData, 0xAAAA, 0, 0, 0),
            CurrentLapNum = 3
        });

        // New session B arrives — lap counter should reset; a lap 1→2 boundary should NOT fire
        manager.ProcessLapData(new PacketLapData
        {
            Header = new PacketHeader(2024, PacketId.LapData, 0xBBBB, 0, 0, 0),
            CurrentLapNum = 1
        });
        manager.ProcessLapData(new PacketLapData
        {
            Header = new PacketHeader(2024, PacketId.LapData, 0xBBBB, 0, 0, 0),
            CurrentLapNum = 2,
            LastLapTimeMs = 85_000
        });

        // Only 1 event (for the first completed lap in session B, lap 1)
        eventBus.Received(1).Publish(Arg.Any<LapCompletedEvent>());
    }
}
