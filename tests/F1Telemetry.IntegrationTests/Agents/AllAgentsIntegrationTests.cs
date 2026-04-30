using F1Telemetry.Agents.Agents;
using F1Telemetry.Contracts;
using F1Telemetry.IntegrationTests.Harness;
using FluentAssertions;

namespace F1Telemetry.IntegrationTests.Agents;

[Trait("Category", "Integration")]
public sealed class AllAgentsIntegrationTests
{
    [Fact]
    public async Task AllAgents_WithRichContext_EachProducesAFinding()
    {
        CompletedLap current = BuildLap(lapTimeMs: 85_320, s1: 28_450, s2: 29_870, s3: 27_000);
        CompletedLap pb = BuildLap(lapTimeMs: 84_920, s1: 28_100, s2: 29_650, s3: 27_170);
        List<TelemetryFrame> trace = BuildRichTrace(60);

        LapAnalysisContext context = new(current, pb, trace, trace);

        AgentFinding?[] results = await Task.WhenAll(
            new DeltaAgent(Stub("""{"finding":"Lose time in S1","estimated_gain_ms":350}""")).AnalyseAsync(context),
            new BrakingAgent(Stub("""{"finding":"Late braking S2","estimated_gain_ms":200}""")).AnalyseAsync(context),
            new CornerAgent(Stub("""{"finding":"Throttle pickup late","estimated_gain_ms":180}""")).AnalyseAsync(context),
            new TyreAgent(Stub("""{"finding":"FL overheating","estimated_gain_ms":150}""")).AnalyseAsync(context),
            new RacingLineAgent(Stub("""{"finding":"Wide entry hairpin","estimated_gain_ms":120}""")).AnalyseAsync(context));

        List<AgentFinding> findings = [.. results.OfType<AgentFinding>()];

        findings.Should().HaveCount(5);
        findings.Select(f => f.AgentName).Should().BeEquivalentTo(
            ["DeltaAgent", "BrakingAgent", "CornerAgent", "TyreAgent", "RacingLineAgent"]);

        List<AgentFinding> ranked = [.. findings.OrderByDescending(f => f.EstimatedGainMs)];
        ranked[0].EstimatedGainMs.Should().Be(350);
        ranked[^1].EstimatedGainMs.Should().Be(120);
    }

    [Fact]
    public async Task SpecialistAgents_WithEmptyTrace_AllReturnNull()
    {
        CompletedLap current = BuildLap(lapTimeMs: 85_320, s1: 28_450, s2: 29_870, s3: 27_000);
        CompletedLap pb = BuildLap(lapTimeMs: 84_920, s1: 28_100, s2: 29_650, s3: 27_170);
        LapAnalysisContext emptyContext = new(current, pb, [], []);
        StubLlmClient stub = new();

        AgentFinding?[] results = await Task.WhenAll(
            new BrakingAgent(stub).AnalyseAsync(emptyContext),
            new CornerAgent(stub).AnalyseAsync(emptyContext),
            new TyreAgent(stub).AnalyseAsync(emptyContext),
            new RacingLineAgent(stub).AnalyseAsync(emptyContext));

        results.Should().AllSatisfy(f => f.Should().BeNull());
    }

    private static StubLlmClient Stub(string response) =>
        new() { ResponseToReturn = response };

    private static CompletedLap BuildLap(int lapTimeMs, uint s1, uint s2, uint s3) =>
        new(SessionId.From(0xDEAD000000000001UL), 1,
            TimeSpan.FromMilliseconds(lapTimeMs),
            TimeSpan.FromMilliseconds(s1),
            TimeSpan.FromMilliseconds(s2),
            TimeSpan.FromMilliseconds(s3),
            true, []);

    private static List<TelemetryFrame> BuildRichTrace(int count)
    {
        SessionId sid = SessionId.From(0xDEAD000000000001UL);
        List<TelemetryFrame> frames = new(count);
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            frames.Add(new TelemetryFrame(sid, 1, t * 90f,
                SpeedKmh: 80f + 120f * MathF.Sin(t * MathF.PI),
                Throttle: t > 0.4f ? 0.9f : 0f,
                Brake: t < 0.3f ? 0.6f : 0f,
                Gear: 4, EngineRpm: 10000, Drs: false,
                TyreTempFl: 105f, TyreTempFr: 98f, TyreTempRl: 88f, TyreTempRr: 89f,
                WorldPositionX: 100f + 50f * MathF.Sin(t * MathF.PI),
                WorldPositionY: 0f,
                WorldPositionZ: 200f + 30f * MathF.Cos(t * MathF.PI)));
        }

        return frames;
    }
}
