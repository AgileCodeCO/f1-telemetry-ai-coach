# .NET Guidelines

Coding standards and best practices for the F1 Telemetry AI Coach project. These guidelines reflect .NET 10 idioms and are enforced through `EditorConfig`, Roslyn analyzers, and code review.

---

## Project Structure

### One responsibility per project

Each project in the solution has a single, clear role. Dependencies flow in one direction only:

```
F1Telemetry.App
  └── F1Telemetry.Agents
  └── F1Telemetry.Storage
  └── F1Telemetry.Ingestion
        └── F1Telemetry.Contracts   ← no dependencies on other projects
```

`F1Telemetry.Contracts` defines interfaces and domain types. No other project may depend on a concrete implementation from a sibling project — only on contracts.

### Folder layout within each project

```
F1Telemetry.Ingestion/
├── Services/
│   ├── UdpListenerService.cs
│   └── SessionManager.cs
├── Parsing/
│   ├── PacketParser.cs
│   └── Structs/
│       ├── PacketHeader.cs
│       └── PacketCarTelemetryData.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
└── F1Telemetry.Ingestion.csproj
```

All service registrations live in a single `AddXxx(this IServiceCollection)` extension method per project. `Program.cs` calls these extensions and contains no business logic.

---

## Dependency Injection

### Always inject interfaces, never concrete types

```csharp
// Correct
public class AgentOrchestrator(ILlmClient llmClient, ILapRepository lapRepository) { }

// Wrong — creates a tight coupling to a specific implementation
public class AgentOrchestrator(OllamaLlmClient llmClient, SqliteLapRepository lapRepo) { }
```

### Prefer primary constructors (.NET 10)

```csharp
public sealed class DeltaAgent(
    ILapRepository lapRepository,
    ITelemetryRepository telemetryRepository,
    ILlmClient llmClient,
    ILogger<DeltaAgent> logger) : IAnalysisAgent
{
    public async Task<AgentFinding> AnalyzeAsync(LapAnalysisContext context, CancellationToken ct)
    {
        // use lapRepository, telemetryRepository, etc.
    }
}
```

### Lifetime rules

| Lifetime | When to use |
|---|---|
| `Singleton` | Stateless services, shared caches, `IPacketParser`, `ILlmClient` |
| `Scoped` | Per-request state in the Blazor/web layer, `TelemetryState` |
| `Transient` | Lightweight, cheap-to-construct objects with no shared state |

Never inject a `Scoped` service into a `Singleton` — use `IServiceScopeFactory` if a singleton needs scoped access.

---

## Async and Concurrency

### Always use `CancellationToken`

Every public async method accepts and forwards a `CancellationToken`. Never use `CancellationToken.None` at a call site unless you explicitly mean "uninterruptible."

```csharp
public async Task<LapCoachingReport> AnalyzeAsync(CompletedLap lap, CancellationToken ct)
{
    var findings = await Task.WhenAll(
        _brakingAgent.AnalyzeAsync(context, ct),
        _cornerAgent.AnalyzeAsync(context, ct),
        _tyreAgent.AnalyzeAsync(context, ct),
        _deltaAgent.AnalyzeAsync(context, ct),
        _lineAgent.AnalyzeAsync(context, ct)
    );
    return new LapCoachingReport(lap, [.. findings.OrderByDescending(f => f.EstimatedGainMs)]);
}
```

### Use `Channel<T>` for producer/consumer pipelines

`System.Threading.Channels` is the correct tool for the UDP-to-parser pipeline. Prefer `Channel.CreateBounded<T>` with a `BoundedChannelFullMode.DropOldest` or `Wait` policy to avoid unbounded memory growth.

```csharp
var channel = Channel.CreateBounded<RawPacket>(new BoundedChannelOptions(4096)
{
    FullMode = BoundedChannelFullMode.DropOldest,
    SingleReader = true,
    SingleWriter = true
});
```

### Never use `async void`

`async void` methods swallow exceptions. The only permitted use is Blazor event handlers (`@onclick`) where the framework requires it, and even then, wrap the body in a try/catch and log.

```csharp
// Correct for Blazor event handlers
private async void OnLapSelected(int lapId)
{
    try { await LoadLapAsync(lapId); }
    catch (Exception ex) { _logger.LogError(ex, "Failed to load lap {LapId}", lapId); }
}
```

### Avoid `.Result` and `.Wait()`

These block a thread and can cause deadlocks. Use `await` everywhere. In truly synchronous contexts (such as EF Core migrations or tool scripts), use `GetAwaiter().GetResult()` only if you fully understand the implications.

---

## Memory and Performance

### Avoid allocations in the hot path

The UDP receive loop and packet parser execute at up to 60Hz. Minimize heap allocations:

```csharp
// Rent from the array pool — return in finally
byte[] buffer = ArrayPool<byte>.Shared.Rent(PacketParser.MaxPacketSize);
try
{
    int received = await _udpClient.ReceiveAsync(buffer.AsMemory(), ct);
    var packet = _parser.Parse(buffer.AsSpan(0, received));
    await _channel.Writer.WriteAsync(packet, ct);
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### Use `Span<T>` and `Memory<T>` for packet parsing

`MemoryMarshal.Read<T>` deserializes struct fields from a span with zero allocation. The packet structs must use `[StructLayout(LayoutKind.Sequential, Pack = 1)]` to match the wire format exactly.

```csharp
public static PacketCarTelemetryData ReadCarTelemetry(ReadOnlySpan<byte> data)
{
    return MemoryMarshal.Read<PacketCarTelemetryData>(data);
}
```

### Throttle SignalR pushes

The live telemetry runs at 60Hz from the PS5. Push to the browser at 10Hz — the chart cannot render faster and the client-side JavaScript overhead is significant. Use a `_frameCount % 6 == 0` gate or a `PeriodicTimer` in the hub.

---

## Error Handling

### Use result types for expected failures

Avoid throwing exceptions for predictable outcomes (packet validation failures, unknown packet IDs). Use `OneOf<T, Error>` or a simple `Result<T>` type.

```csharp
public record struct ParseResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static ParseResult<T> Ok(T value) => new(value, null);
    public static ParseResult<T> Fail(string error) => new(default, error);
}
```

### Only catch what you can handle

```csharp
// Correct — specific exception, meaningful recovery
catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
{
    _logger.LogWarning("UDP socket reset; rebinding");
    await RebindAsync(ct);
}

// Wrong — swallows everything
catch (Exception) { }
```

### Structured logging — always include context

```csharp
// Correct — lap number and session ID are queryable fields in the log
_logger.LogInformation("Lap {LapNumber} completed in {LapTimeMs}ms for session {SessionId}",
    lap.LapNumber, lap.LapTimeMs, lap.SessionId);

// Wrong — interpolated string prevents structured logging
_logger.LogInformation($"Lap {lap.LapNumber} completed");
```

---

## Domain Modeling

### Prefer records for immutable domain objects

```csharp
public sealed record CompletedLap(
    string SessionId,
    int LapNumber,
    TimeSpan LapTime,
    TimeSpan Sector1,
    TimeSpan Sector2,
    TimeSpan Sector3,
    bool IsValid,
    IReadOnlyList<TelemetryFrame> Frames);

public sealed record AgentFinding(
    string AgentName,
    AnalysisCategory Category,
    string Explanation,
    int EstimatedGainMs);
```

### Use strongly-typed IDs to prevent ID confusion

```csharp
public readonly record struct SessionId(string Value)
{
    public override string ToString() => Value;
    public static SessionId From(ulong rawUid) => new(rawUid.ToString("X16"));
}
```

Passing `string` or `int` for IDs across layers makes it easy to accidentally pass the wrong ID type. Strongly-typed IDs make such mistakes a compile error.

### Enums for packet types and categories

```csharp
public enum PacketId : byte
{
    Motion = 0,
    Session = 1,
    LapData = 2,
    CarTelemetry = 6,
    CarStatus = 7,
    CarDamage = 10
}

public enum AnalysisCategory
{
    Braking,
    Corner,
    Tyre,
    Delta,
    RacingLine
}
```

---

## Configuration

### Use the Options pattern — never read `IConfiguration` directly in business logic

```csharp
// Define a typed options class
public sealed class LlmOptions
{
    public const string SectionName = "LLM";
    public string Provider { get; set; } = "ollama";
    public string Model { get; set; } = "llama3.2";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public int TimeoutSeconds { get; set; } = 30;
}

// Register with validation
services.AddOptions<LlmOptions>()
    .BindConfiguration(LlmOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Inject where needed
public class LlmClientFactory(IOptions<LlmOptions> options) { }
```

### Validate options on startup

Use `.ValidateOnStart()` so misconfiguration fails immediately rather than at runtime when the first request arrives. Add `[Required]` and `[Url]` annotations to your options properties to enable automatic validation.

---

## Blazor-Specific Guidelines

### Prefer `@inject` over constructor injection in components

Blazor components use property injection via `@inject`. Components should not have constructors for DI.

```razor
@inject ITelemetryState TelemetryState
@inject NavigationManager Navigation
@inject ILogger<LapReviewPage> Logger
```

### Use `StateHasChanged()` sparingly

Only call `StateHasChanged()` when you mutate component state outside of Blazor event callbacks (e.g., from a SignalR message handler). Wrap it in `InvokeAsync` when called from a non-UI thread:

```csharp
HubConnection.On<CoachingReportDto>("CoachingReportReady", async report =>
{
    _report = report;
    await InvokeAsync(StateHasChanged);
});
```

### Dispose `HubConnection` correctly

Components that open a SignalR connection must implement `IAsyncDisposable`:

```csharp
@implements IAsyncDisposable

@code {
    private HubConnection? _hubConnection;

    protected override async Task OnInitializedAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/telemetry"))
            .WithAutomaticReconnect()
            .Build();
        await _hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}
```

### Keep components small and composed

A component longer than ~100 lines of `@code` should be split. Extract chart rendering, coaching card rendering, and session table rows as separate `*Component.razor` files.

---

## Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Interfaces | `I` prefix + noun | `ILlmClient`, `ILapRepository` |
| Abstract base classes | `Base` suffix | `BaseAnalysisAgent` |
| Background services | `Service` suffix | `UdpListenerService` |
| Event records | `Event` suffix | `LapCompletedEvent` |
| DTOs | `Dto` suffix | `CoachingReportDto` |
| Options classes | `Options` suffix | `LlmOptions` |
| Blazor pages | `Page` suffix | `LapReviewPage.razor` |
| Blazor components | `Component` suffix | `CoachingCardComponent.razor` |
| Constants | `PascalCase` in `static class` | `PacketConstants.MaxPacketSize` |
| Private fields | `_camelCase` | `_logger`, `_channel` |

---

## Analyzers and Tooling

Add these to `Directory.Build.props` to enforce standards across all projects:

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <AnalysisMode>All</AnalysisMode>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <LangVersion>preview</LangVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="10.*" />
  <PackageReference Include="Roslynator.Analyzers" Version="4.*" />
</ItemGroup>
```

Nullable reference types are enabled globally. All warnings are errors. This forces explicit null handling throughout the codebase and catches common mistakes at compile time.
