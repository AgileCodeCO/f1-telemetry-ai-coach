using F1Telemetry.App.Dtos;
using F1Telemetry.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace F1Telemetry.IntegrationTests.App;

[Trait("Category", "Integration")]
public sealed class SignalRHubTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _dbPath;
    private readonly WebApplicationFactory<Program> _factory;

    public SignalRHubTests(WebApplicationFactory<Program> factory)
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f1-test-{Guid.NewGuid():N}.db");

        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}");
            b.UseSetting("LLM:Provider", "ollama");
            b.UseSetting("Udp:Port", "0");
        });
    }

    [Fact]
    public async Task CoachingReportReady_WhenEventPublished_ClientReceivesMessage()
    {
        HttpClient httpClient = _factory.CreateClient();
        Uri baseAddress = httpClient.BaseAddress!;
        Uri hubUri = new(baseAddress, "/hubs/telemetry");

        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(hubUri, opts =>
            {
                opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        CoachingReportDto? received = null;
        var tcs = new TaskCompletionSource<CoachingReportDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<CoachingReportDto>("CoachingReportReady", dto =>
        {
            tcs.TrySetResult(dto);
        });

        await connection.StartAsync();

        IEventBus eventBus = _factory.Services.GetRequiredService<IEventBus>();
        CompletedLap lap = new(
            SessionId: SessionId.From(1),
            LapNumber: 3,
            LapTime: TimeSpan.FromSeconds(90),
            Sector1: TimeSpan.FromSeconds(30),
            Sector2: TimeSpan.FromSeconds(30),
            Sector3: TimeSpan.FromSeconds(30),
            IsValid: true,
            Frames: []);

        AgentFinding finding = new("DeltaAgent", AnalysisCategory.Delta, "You lost 0.3s in T4.", 300);
        LapCoachingReport report = new(lap, [finding]);

        eventBus.Publish(new CoachingReportReadyEvent(report));

        received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        received.LapNumber.Should().Be(3);
        received.Findings.Should().HaveCount(1);
        received.Findings[0].AgentName.Should().Be("DeltaAgent");

        await connection.DisposeAsync();
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
