using F1Telemetry.Agents.Extensions;
using F1Telemetry.Ingestion.Extensions;
using F1Telemetry.Storage.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIngestion();
builder.Services.AddStorage(builder.Configuration);
builder.Services.AddAgents(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
