using F1Telemetry.Agents.Agents;
using F1Telemetry.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace F1Telemetry.UnitTests.Agents;

public sealed class CornerAgentTests
{
    private static readonly string ValidJson =
        """{"finding":"Throttle pickup too late in S2","estimated_gain_ms":180}""";

    [Fact]
    public async Task AnalyseAsync_WhenNoPb_ReturnsNull()
    {
        CornerAgent agent = new(Substitute.For<IChatClient>());
        AgentFinding? result = await agent.AnalyseAsync(BuildContext(hasPb: false));
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_WhenEmptyTrace_ReturnsNull()
    {
        CornerAgent agent = new(Substitute.For<IChatClient>());
        AgentFinding? result = await agent.AnalyseAsync(BuildContext(traceSize: 0));
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_HappyPath_ReturnsCornerFinding()
    {
        IChatClient llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ValidJson))));

        CornerAgent agent = new(llm);
        AgentFinding? result = await agent.AnalyseAsync(BuildContext());

        result.Should().NotBeNull();
        result!.AgentName.Should().Be("CornerAgent");
        result.Category.Should().Be(AnalysisCategory.Corner);
        result.EstimatedGainMs.Should().Be(180);
    }

    [Fact]
    public async Task AnalyseAsync_MalformedResponse_ReturnsNull()
    {
        IChatClient llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "bad response"))));

        CornerAgent agent = new(llm);
        AgentFinding? result = await agent.AnalyseAsync(BuildContext());
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_PromptContainsSpeedAndThrottleStats()
    {
        string capturedPrompt = string.Empty;
        IChatClient llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedPrompt = call.ArgAt<IEnumerable<ChatMessage>>(0)
                    .FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ValidJson)));
            });

        CornerAgent agent = new(llm);
        await agent.AnalyseAsync(BuildContext());

        capturedPrompt.Should().Contain("min speed");
        capturedPrompt.Should().Contain("on-throttle");
        capturedPrompt.Should().Contain("km/h");
    }

    private static LapAnalysisContext BuildContext(bool hasPb = true, int traceSize = 30)
    {
        CompletedLap current = BuildLap();
        CompletedLap? pb = hasPb ? BuildLap(lapTimeMs: 84_500) : null;
        IReadOnlyList<TelemetryFrame> curTrace = traceSize > 0 ? BuildTrace(traceSize) : [];
        IReadOnlyList<TelemetryFrame> pbTrace = hasPb && traceSize > 0 ? BuildTrace(traceSize) : [];
        return new LapAnalysisContext(current, pb, curTrace, pbTrace);
    }

    private static CompletedLap BuildLap(int lapTimeMs = 85_000) =>
        new(SessionId.From(0xDEAD000000000001UL), 1,
            TimeSpan.FromMilliseconds(lapTimeMs),
            TimeSpan.FromMilliseconds(28_000),
            TimeSpan.FromMilliseconds(30_000),
            TimeSpan.FromMilliseconds(27_000),
            true, []);

    private static List<TelemetryFrame> BuildTrace(int count)
    {
        SessionId sid = SessionId.From(0xDEAD000000000001UL);
        List<TelemetryFrame> frames = new(count);
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            frames.Add(new TelemetryFrame(sid, 1, t * 90f,
                SpeedKmh: 80f + 120f * MathF.Sin(t * MathF.PI),
                Throttle: t > 0.4f ? 0.9f : 0f,
                Brake: t < 0.3f ? 0.5f : 0f,
                Gear: 3, EngineRpm: 9000, Drs: false,
                TyreTempFl: 90f, TyreTempFr: 92f, TyreTempRl: 88f, TyreTempRr: 89f,
                WorldPositionX: 0f, WorldPositionY: 0f, WorldPositionZ: 0f));
        }

        return frames;
    }
}
