using System.Net;
using System.Net.Http.Json;
using F1Telemetry.App.Dtos;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace F1Telemetry.IntegrationTests.App;

[Trait("Category", "Integration")]
public sealed class RestApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _dbPath;
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public RestApiTests(WebApplicationFactory<Program> factory)
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f1-test-{Guid.NewGuid():N}.db");

        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}");
            b.UseSetting("LLM:Provider", "ollama");
            b.UseSetting("Udp:Port", "0");
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetSessions_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSessions_WhenEmpty_ReturnsEmptyArray()
    {
        List<SessionSummaryDto>? sessions =
            await _client.GetFromJsonAsync<List<SessionSummaryDto>>("/api/sessions");

        sessions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetLapsForSession_WhenSessionNotFound_ReturnsEmptyArray()
    {
        List<LapSummaryDto>? laps =
            await _client.GetFromJsonAsync<List<LapSummaryDto>>("/api/sessions/UNKNOWN/laps");

        laps.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetFeedbackForLap_WhenNotFound_ReturnsEmptyArray()
    {
        List<FindingDto>? feedback =
            await _client.GetFromJsonAsync<List<FindingDto>>("/api/sessions/UNKNOWN/laps/1/feedback");

        feedback.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
