using F1Telemetry.Agents.Agents;
using F1Telemetry.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace F1Telemetry.UnitTests.Agents;

public sealed class TyreAgentTests
{
    private static readonly string ValidJson =
        """{"finding":"Front-left overheating in S1","estimated_gain_ms":150}""";

    [Fact]
    public async Task AnalyseAsync_WhenEmptyTrace_ReturnsNull()
    {
        TyreAgent agent = new(Substitute.For<IChatClient>());
        LapAnalysisContext context = new(BuildLap(), null, [], []);
        AgentFinding? result = await agent.AnalyseAsync(context);
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_WhenAllTyreTempsZero_ReturnsNull()
    {
        TyreAgent agent = new(Substitute.For<IChatClient>());
        IReadOnlyList<TelemetryFrame> zeroTrace = BuildTrace(30, tyreTempFl: 0f, tyreTempFr: 0f);
        LapAnalysisContext context = new(BuildLap(), null, zeroTrace, []);
        AgentFinding? result = await agent.AnalyseAsync(context);
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_HappyPath_ReturnsTyreFinding()
    {
        IChatClient llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ValidJson))));

        TyreAgent agent = new(llm);
        LapAnalysisContext context = new(BuildLap(), null, BuildTrace(30), []);

        AgentFinding? result = await agent.AnalyseAsync(context);
        result.Should().NotBeNull();
        result!.AgentName.Should().Be("TyreAgent");
        result.Category.Should().Be(AnalysisCategory.Tyre);
        result.EstimatedGainMs.Should().Be(150);
    }

    [Fact]
    public async Task AnalyseAsync_MalformedResponse_ReturnsNull()
    {
        IChatClient llm = Substitute.For<IChatClient>();
        llm.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "bad response"))));

        TyreAgent agent = new(llm);
        LapAnalysisContext context = new(BuildLap(), null, BuildTrace(30), []);

        AgentFinding? result = await agent.AnalyseAsync(context);
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_PromptContainsTyreTemps()
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

        TyreAgent agent = new(llm);
        LapAnalysisContext context = new(BuildLap(), null, BuildTrace(30), []);
        await agent.AnalyseAsync(context);

        capturedPrompt.Should().Contain("FL");
        capturedPrompt.Should().Contain("°C");
        capturedPrompt.Should().Contain("Sector 1");
    }

    private static CompletedLap BuildLap() =>
        new(SessionId.From(0xDEAD000000000001UL), 1,
            TimeSpan.FromMilliseconds(85_000),
            TimeSpan.FromMilliseconds(28_000),
            TimeSpan.FromMilliseconds(30_000),
            TimeSpan.FromMilliseconds(27_000),
            true, []);

    private static List<TelemetryFrame> BuildTrace(int count, float tyreTempFl = 105f, float tyreTempFr = 98f)
    {
        SessionId sid = SessionId.From(0xDEAD000000000001UL);
        List<TelemetryFrame> frames = new(count);
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            frames.Add(new TelemetryFrame(sid, 1, t * 90f,
                SpeedKmh: 150f, Throttle: 0.8f, Brake: 0f,
                Gear: 5, EngineRpm: 11000, Drs: false,
                TyreTempFl: tyreTempFl, TyreTempFr: tyreTempFr,
                TyreTempRl: 88f, TyreTempRr: 89f,
                WorldPositionX: 0f, WorldPositionY: 0f, WorldPositionZ: 0f));
        }

        return frames;
    }
}
