using F1Telemetry.Agents.Agents;
using F1Telemetry.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace F1Telemetry.UnitTests.Agents;

public sealed class DeltaAgentTests
{
    private static readonly string ValidJson = """{"finding":"Sector 1 is weakest","estimated_gain_ms":350}""";

    private static CompletedLap BuildLap(
        int lapTimeMs = 85_320,
        int s1 = 28_450,
        int s2 = 29_870,
        int s3 = 27_000,
        int lapNumber = 1) =>
        new(SessionId.From(0xDEAD000000000001UL), lapNumber,
            TimeSpan.FromMilliseconds(lapTimeMs),
            TimeSpan.FromMilliseconds(s1),
            TimeSpan.FromMilliseconds(s2),
            TimeSpan.FromMilliseconds(s3),
            true, []);

    [Fact]
    public async Task AnalyseAsync_WhenNoPb_ReturnsNull()
    {
        IChatClient chatClient = Substitute.For<IChatClient>();
        DeltaAgent agent = new(chatClient);
        LapAnalysisContext context = new(BuildLap(), null, [], []);

        AgentFinding? result = await agent.AnalyseAsync(context);

        result.Should().BeNull();
        await chatClient.DidNotReceive().GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyseAsync_HappyPath_ReturnsFinding()
    {
        IChatClient chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ValidJson))));

        DeltaAgent agent = new(chatClient);
        CompletedLap current = BuildLap(lapTimeMs: 85_320, s1: 28_450, s2: 29_870, s3: 27_000);
        CompletedLap pb = BuildLap(lapTimeMs: 84_920, s1: 28_100, s2: 29_650, s3: 27_170);
        LapAnalysisContext context = new(current, pb, [], []);

        AgentFinding? result = await agent.AnalyseAsync(context);

        result.Should().NotBeNull();
        result!.Finding.Should().Be("Sector 1 is weakest");
        result.EstimatedGainMs.Should().Be(350);
        result.AgentName.Should().Be("DeltaAgent");
        result.Category.Should().Be(AnalysisCategory.Delta);
    }

    [Fact]
    public async Task AnalyseAsync_MalformedLlmResponse_ReturnsNull()
    {
        IChatClient chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "not json at all"))));

        DeltaAgent agent = new(chatClient);
        LapAnalysisContext context = new(BuildLap(), BuildLap(lapTimeMs: 84_920), [], []);

        AgentFinding? result = await agent.AnalyseAsync(context);
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_PromptContainsSectorDeltas()
    {
        string capturedUserPrompt = string.Empty;
        IChatClient chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                IEnumerable<ChatMessage> messages = call.ArgAt<IEnumerable<ChatMessage>>(0);
                capturedUserPrompt = messages.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ValidJson)));
            });

        DeltaAgent agent = new(chatClient);
        CompletedLap current = BuildLap(lapTimeMs: 85_320, s1: 28_450, s2: 29_870, s3: 27_000);
        CompletedLap pb = BuildLap(lapTimeMs: 84_920, s1: 28_100, s2: 29_650, s3: 27_170);
        await agent.AnalyseAsync(new LapAnalysisContext(current, pb, [], []));

        capturedUserPrompt.Should().Contain("+350ms");
        capturedUserPrompt.Should().Contain("+220ms");
        capturedUserPrompt.Should().Contain("-170ms");
    }
}
