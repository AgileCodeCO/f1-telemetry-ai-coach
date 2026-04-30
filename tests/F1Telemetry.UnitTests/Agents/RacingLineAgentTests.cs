using F1Telemetry.Agents.Agents;
using F1Telemetry.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace F1Telemetry.UnitTests.Agents;

public sealed class RacingLineAgentTests
{
    private static readonly string ValidJson =
        """{"finding":"Wide entry at S2 hairpin","estimated_gain_ms":120}""";

    [Fact]
    public async Task AnalyseAsync_WhenNoPb_ReturnsNull()
    {
        RacingLineAgent agent = new(Substitute.For<IChatClient>());
        LapAnalysisContext context = new(BuildLap(), null, BuildTrace(30, hasWorldPos: true), []);
        AgentFinding? result = await agent.AnalyseAsync(context);
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_WhenEmptyTrace_ReturnsNull()
    {
        RacingLineAgent agent = new(Substitute.For<IChatClient>());
        LapAnalysisContext context = new(BuildLap(), BuildLap(), [], []);
        AgentFinding? result = await agent.AnalyseAsync(context);
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_WhenAllWorldPositionsZero_ReturnsNull()
    {
        RacingLineAgent agent = new(Substitute.For<IChatClient>());
        IReadOnlyList<TelemetryFrame> zeroTrace = BuildTrace(30, hasWorldPos: false);
        LapAnalysisContext context = new(BuildLap(), BuildLap(), zeroTrace, zeroTrace);
        AgentFinding? result = await agent.AnalyseAsync(context);
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_HappyPath_ReturnsRacingLineFinding()
    {
        IChatClient llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ValidJson))));

        RacingLineAgent agent = new(llm);
        IReadOnlyList<TelemetryFrame> trace = BuildTrace(30, hasWorldPos: true);
        LapAnalysisContext context = new(BuildLap(), BuildLap(), trace, trace);

        AgentFinding? result = await agent.AnalyseAsync(context);
        result.Should().NotBeNull();
        result!.AgentName.Should().Be("RacingLineAgent");
        result.Category.Should().Be(AnalysisCategory.RacingLine);
        result.EstimatedGainMs.Should().Be(120);
    }

    [Fact]
    public async Task AnalyseAsync_MalformedResponse_ReturnsNull()
    {
        IChatClient llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "bad response"))));

        RacingLineAgent agent = new(llm);
        IReadOnlyList<TelemetryFrame> trace = BuildTrace(30, hasWorldPos: true);
        LapAnalysisContext context = new(BuildLap(), BuildLap(), trace, trace);

        AgentFinding? result = await agent.AnalyseAsync(context);
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_PromptContainsPositionSpread()
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

        RacingLineAgent agent = new(llm);
        IReadOnlyList<TelemetryFrame> trace = BuildTrace(30, hasWorldPos: true);
        LapAnalysisContext context = new(BuildLap(), BuildLap(), trace, trace);
        await agent.AnalyseAsync(context);

        capturedPrompt.Should().Contain("line width");
        capturedPrompt.Should().Contain("Sector 1");
    }

    private static CompletedLap BuildLap() =>
        new(SessionId.From(0xDEAD000000000001UL), 1,
            TimeSpan.FromMilliseconds(85_000),
            TimeSpan.FromMilliseconds(28_000),
            TimeSpan.FromMilliseconds(30_000),
            TimeSpan.FromMilliseconds(27_000),
            true, []);

    private static List<TelemetryFrame> BuildTrace(int count, bool hasWorldPos = true)
    {
        SessionId sid = SessionId.From(0xDEAD000000000001UL);
        List<TelemetryFrame> frames = new(count);
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float x = hasWorldPos ? 100f + 50f * MathF.Sin(t * MathF.PI) : 0f;
            float z = hasWorldPos ? 200f + 30f * MathF.Cos(t * MathF.PI) : 0f;
            frames.Add(new TelemetryFrame(sid, 1, t * 90f,
                SpeedKmh: 150f, Throttle: 0.8f, Brake: 0f,
                Gear: 5, EngineRpm: 11000, Drs: false,
                TyreTempFl: 90f, TyreTempFr: 92f, TyreTempRl: 88f, TyreTempRr: 89f,
                WorldPositionX: x, WorldPositionY: 0f, WorldPositionZ: z));
        }

        return frames;
    }
}
