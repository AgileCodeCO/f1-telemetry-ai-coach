# CLAUDE.md

This file provides context and instructions for Claude when working on the F1 Telemetry AI Coach project. Read this before making any changes to the codebase.

---

## Project Purpose

A local-first telemetry analysis platform for F1 simulation racing on PlayStation 5. The PS5 broadcasts UDP telemetry; this application receives it, stores it locally, runs AI agents to analyze each lap, and surfaces coaching feedback through a Blazor dashboard — all on a single laptop with no cloud infrastructure required.

---

## Essential Reading

Before writing or modifying any code, consult the relevant document:

| Topic | Document |
|---|---|
| System design, layers, data flow, DB schemas | [`docs/architecture.md`](./docs/architecture.md) |
| Sprint breakdown, task lists, milestones | [`docs/implementation-plan.md`](./docs/implementation-plan.md) |
| Coding standards, patterns, naming rules | [`docs/dotnet-guidelines.md`](./docs/dotnet-guidelines.md) |
| Unit tests, integration tests, UDP simulator | [`docs/testing-strategy.md`](./docs/testing-strategy.md) |

---

## Tech Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 10 SDK |
| Backend host | ASP.NET Core (Kestrel, `localhost:5291`) |
| Frontend | Blazor Server |
| Real-time push | SignalR (library, not Azure service) |
| High-frequency time-series | InfluxDB OSS 2.7 (`localhost:8086`) |
| Relational / summaries | SQLite via EF Core |
| Lap archive | Local filesystem JSON (`~/f1telemetry/sessions/`) |
| AI agents | Custom orchestrator + specialist pattern |
| LLM abstraction | `Microsoft.Extensions.AI.IChatClient` (v10.5.0) |
| LLM backend | Pluggable — Ollama (OllamaSharp), OpenAI, Anthropic, LM Studio |
| Testing | xUnit, NSubstitute, FluentAssertions, Coverlet |
| Infrastructure | Docker Compose (InfluxDB only) |

---

## Solution Structure

```
f1-telemetry/
├── src/
│   ├── F1Telemetry.Contracts/     # Interfaces, DTOs, domain records — no dependencies
│   ├── F1Telemetry.Ingestion/     # UDP listener, packet parser, session manager
│   ├── F1Telemetry.Storage/       # InfluxDB, SQLite, and file archive repositories
│   ├── F1Telemetry.Agents/        # AI agents, orchestrator, IChatClient providers
│   └── F1Telemetry.App/           # Blazor pages, SignalR hub, REST API, Program.cs
├── tests/
│   ├── F1Telemetry.UnitTests/
│   └── F1Telemetry.IntegrationTests/
├── docs/
├── data/                          # Runtime data — gitignored
├── docker-compose.yml
├── Directory.Build.props
├── global.json
└── CLAUDE.md
```

Dependencies flow in one direction only: `App → Agents → Storage → Ingestion → Contracts`. No project may reference a sibling's concrete types — only interfaces from `Contracts`.

---

## Key Architecture Decisions

**`IChatClient` is the LLM abstraction.** Never call an LLM provider SDK directly from an agent. Always go through `Microsoft.Extensions.AI.IChatClient`. The active implementation is selected at startup from `LLM.Provider` in `appsettings.json`. Adding a new provider means wiring a new `IChatClient` in `ServiceCollectionExtensions` — no agent code changes. Ollama uses `OllamaSharp.OllamaApiClient`; OpenAI and LM Studio use `OpenAI.Chat.ChatClient.AsIChatClient()`; Anthropic uses the internal `AnthropicChatClient` wrapper (no official adapter exists yet).

**Channel&lt;T&gt; is the ingestion pipeline.** The UDP receive loop writes to a bounded `Channel<RawPacket>` (capacity 4096, `DropOldest`). Downstream consumers read from it independently. Do not introduce any other queue or buffer between the listener and the parser.

**Lap boundary drives everything downstream.** The `SessionManager` detects when `PacketLapData.currentLapNum` increments and publishes a `LapCompletedEvent`. All storage writes and all AI agent invocations are triggered by this event — never by a timer or a poll.

**InfluxDB for traces, SQLite for summaries.** High-frequency per-frame data (speed, throttle, brake, tyre temps) goes to InfluxDB tagged by `session_uid` and `lap_number`. Lap-level aggregates and AI feedback text go to SQLite. Never store frame data in SQLite or summary data in InfluxDB.

**Blazor Server, not Blazor WebAssembly.** The app runs on the same machine as the data, so there is no reason to ship code to the browser. Use `@inject` for dependencies, `IAsyncDisposable` on components that open SignalR connections, and `InvokeAsync(StateHasChanged)` when mutating state from non-UI threads.

---

## Coding Rules (Summary)

Full rules are in [`docs/dotnet-guidelines.md`](./docs/dotnet-guidelines.md). The non-negotiables:

- **Nullable enabled, warnings as errors.** `Directory.Build.props` enforces this globally. Fix nullability warnings; do not suppress them.
- **Always pass `CancellationToken`.** Every public async method must accept and forward a `CancellationToken`. No `CancellationToken.None` at call sites.
- **No `async void`.** Blazor event handlers are the only exception — wrap their bodies in try/catch and log.
- **Inject interfaces, never concretes.** Constructor parameters and `@inject` directives reference interfaces from `Contracts` only.
- **Structured logging only.** Use message templates with named parameters (`{LapNumber}`, `{SessionId}`). Never use string interpolation in log calls.
- **Records for domain objects.** `CompletedLap`, `AgentFinding`, `LapCoachingReport`, and all DTOs are `sealed record` types.
- **Primary constructors.** Use C# primary constructor syntax for all service classes.
- **Result types over exceptions.** Expected failures (unknown packet ID, missing personal best) return `ParseResult<T>` or nullable — they do not throw.

---

## Testing Rules (Summary)

Full rules are in [`docs/testing-strategy.md`](./docs/testing-strategy.md). The non-negotiables:

- **70% line coverage minimum** across all non-Contracts projects. The build fails below this threshold.
- **Unit tests use NSubstitute mocks.** Never make real network calls, real database calls, or real LLM calls in a unit test.
- **SQLite unit tests use in-memory mode** (`Data Source=:memory:`), not the EF Core in-memory provider.
- **Integration tests are tagged** `[Trait("Category", "Integration")]` and excluded from the default `dotnet test` run.
- **The `UdpGameSimulator`** in `F1Telemetry.IntegrationTests/Harness/` is the only approved way to simulate PS5 telemetry in tests. Do not open real UDP sockets in unit tests.
- **Agents are always tested with `StubLlmClient`** in integration tests. Never call a real LLM in CI.
- **Every new agent gets a unit test** covering: happy path with a mock LLM response, no personal best lap available, and malformed LLM response handling.

---

## Running the Project

```bash
# Start InfluxDB (required)
docker compose up -d influxdb

# Run the app (UDP listener + Blazor dashboard)
dotnet run --project src/F1Telemetry.App

# Open the dashboard
start http://localhost:5291
```

```bash
# Unit tests only (no Docker needed)
dotnet test --filter "Category!=Integration"

# Integration tests (requires Docker with InfluxDB)
dotnet test --filter "Category=Integration"

# All tests with coverage report
dotnet test --collect:"XPlat Code Coverage"
```

---

## Configuration

The LLM backend is swapped via `appsettings.json` — no code changes required:

```json
{
  "LLM": {
    "Provider": "ollama",
    "Model": "llama3.2",
    "BaseUrl": "http://localhost:11434",
    "TimeoutSeconds": 30
  }
}
```

Valid `Provider` values: `ollama` · `openai` · `anthropic` · `lmstudio`

For development without a PS5, set `Storage.ReplayMode: true` in `appsettings.Development.json` to feed archived session JSON through the pipeline instead of live UDP.

---

## What Not to Do

- Do not add any Azure SDK, Azure Service Bus, Azure Storage, or Azure SignalR references. All infrastructure is local.
- Do not read `IConfiguration` directly in business logic classes. Use the Options pattern (`IOptions<T>`).
- Do not store telemetry frame data in SQLite. It belongs in InfluxDB.
- Do not call `StateHasChanged()` from a background thread without wrapping it in `InvokeAsync`.
- Do not introduce a new NuGet package without checking `Directory.Build.props` for an existing version pin.
- Do not skip writing tests for a new agent or repository — coverage thresholds are enforced in CI.
