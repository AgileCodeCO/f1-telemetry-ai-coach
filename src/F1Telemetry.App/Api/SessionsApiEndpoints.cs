using F1Telemetry.App.Dtos;
using F1Telemetry.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace F1Telemetry.App.Api;

internal static class SessionsApiEndpoints
{
    internal static IEndpointRouteBuilder MapSessionsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sessions", async (ILapRepository repo, CancellationToken ct) =>
        {
            IReadOnlyList<SessionSummary> sessions = await repo.GetAllSessionsAsync(ct);
            return sessions.Select(SessionSummaryDto.FromDomain);
        });

        app.MapGet("/api/sessions/{sessionId}/laps", async (
            string sessionId,
            ILapRepository repo,
            CancellationToken ct) =>
        {
            IReadOnlyList<CompletedLap> laps =
                await repo.GetLapsBySessionAsync(new SessionId(sessionId), ct);
            return laps.Select(LapSummaryDto.FromDomain);
        });

        app.MapGet("/api/sessions/{sessionId}/laps/{lapNumber:int}/feedback", async (
            string sessionId,
            int lapNumber,
            ILapRepository repo,
            CancellationToken ct) =>
        {
            IReadOnlyList<AgentFinding> findings =
                await repo.GetFeedbackAsync(new SessionId(sessionId), lapNumber, ct);
            return findings.Select(FindingDto.FromDomain);
        });

        return app;
    }
}
