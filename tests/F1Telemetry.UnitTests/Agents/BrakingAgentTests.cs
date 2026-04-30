using F1Telemetry.Agents.Agents;
using F1Telemetry.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace F1Telemetry.UnitTests.Agents;

public sealed class BrakingAgentTests
{
    private static readonly string ValidJson =
        """{"finding":"Late braking in S1","estimated_gain_ms":200}""";

    private static LapAnalysisContext BuildContext(bool hasPb = true, int traceSize = 30)
    {
        CompletedLap current = BuildLap();
        CompletedLap? pb = hasPb ? BuildLap(lapTimeMs: 84_500) : null;
        IReadOnlyList<TelemetryFrame> curTrace = traceSize > 0 ? BuildTrace(traceSize) : [];
        IReadOnlyList<TelemetryFrame> pbTrace = hasPb && traceSize > 0 ? BuildTrace(traceSize) : [];
        return new LapAnalysisContext(current, pb, curTrace, pbTrace);
    }

    [Fact]
    public async Task AnalyseAsync_WhenNoPb_ReturnsNull()
    {
        BrakingAgent agent = new(Substitute.For<IChatClient>());
        AgentFinding? result = await agent.AnalyseAsync(BuildContext(hasPb: false));
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_WhenEmptyTrace_ReturnsNull()
    {
        BrakingAgent agent = new(Substitute.For<IChatClient>());
        AgentFinding? result = await agent.AnalyseAsync(BuildContext(traceSize: 0));
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_HappyPath_ReturnsBrakingFinding()
    {
        IChatClient llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ValidJson))));

        BrakingAgent agent = new(llm);
        AgentFinding? result = await agent.AnalyseAsync(BuildContext());

        result.Should().NotBeNull();
        result!.AgentName.Should().Be("BrakingAgent");
        result.Category.Should().Be(AnalysisCategory.Braking);
        result.EstimatedGainMs.Should().Be(200);
    }

    [Fact]
    public async Task AnalyseAsync_MalformedResponse_ReturnsNull()
    {
        IChatClient llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "not json"))));

        BrakingAgent agent = new(llm);
        AgentFinding? result = await agent.AnalyseAsync(BuildContext());
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_PromptContainsBrakeStats()
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

        BrakingAgent agent = new(llm);
        await agent.AnalyseAsync(BuildContext());

        capturedPrompt.Should().Contain("Sector 1");
        capturedPrompt.Should().Contain("peak");
        capturedPrompt.Should().Contain("braking");
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
                SpeedKmh: 150f + 50f * t,
                Throttle: t < 0.5f ? 1f : 0f,
                Brake: t >= 0.5f ? 0.8f : 0f,
                Gear: 4, EngineRpm: 10000, Drs: false,
                TyreTempFl: 90f, TyreTempFr: 92f, TyreTempRl: 88f, TyreTempRr: 89f,
                WorldPositionX: 0f, WorldPositionY: 0f, WorldPositionZ: 0f));
        }

        return frames;
    }
}
