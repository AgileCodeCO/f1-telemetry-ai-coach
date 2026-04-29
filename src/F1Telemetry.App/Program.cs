using F1Telemetry.Ingestion.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIngestion();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
