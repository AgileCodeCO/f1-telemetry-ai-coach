using F1Telemetry.Agents.Agents;
using F1Telemetry.Contracts;
using F1Telemetry.IntegrationTests.Harness;
using FluentAssertions;

namespace F1Telemetry.IntegrationTests.Agents;

[Trait("Category", "Integration")]
public sealed class DeltaAgentIntegrationTests
{
    [Fact]
    public async Task AnalyseAsync_PromptContainsSectorDeltas_ForTwoKnownLaps()
    {
        // Two laps with known sector times for deterministic delta assertions
        CompletedLap current = BuildLap(lapTime: 85_320, s1: 28_450, s2: 29_870, s3: 27_000);
        CompletedLap pb = BuildLap(lapTime: 84_920, s1: 28_100, s2: 29_650, s3: 27_170);
        LapAnalysisContext context = new(current, pb, [], []);
        StubLlmClient stub = new();
        DeltaAgent agent = new(stub);

        AgentFinding? finding = await agent.AnalyseAsync(context);

        stub.LastUserPrompt.Should().Contain("+350ms");  // S1 delta: 28450 - 28100
        stub.LastUserPrompt.Should().Contain("+220ms");  // S2 delta: 29870 - 29650
        stub.LastUserPrompt.Should().Contain("-170ms");  // S3 delta: 27000 - 27170 (current is faster)
        finding.Should().NotBeNull();
        finding!.EstimatedGainMs.Should().Be(350);
    }

    [Fact]
    public async Task AnalyseAsync_NoPb_ReturnsNull()
    {
        CompletedLap current = BuildLap(lapTime: 85_000, s1: 28_000, s2: 30_000, s3: 27_000);
        LapAnalysisContext context = new(current, null, [], []);
        DeltaAgent agent = new(new StubLlmClient());

        AgentFinding? finding = await agent.AnalyseAsync(context);
        finding.Should().BeNull();
    }

    private static CompletedLap BuildLap(uint lapTime, uint s1, uint s2, uint s3) =>
        new(SessionId.From(0xDEAD000000000001UL), 1,
            TimeSpan.FromMilliseconds(lapTime),
            TimeSpan.FromMilliseconds(s1),
            TimeSpan.FromMilliseconds(s2),
            TimeSpan.FromMilliseconds(s3),
            true, []);
}
