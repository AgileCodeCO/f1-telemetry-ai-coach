# F1 Telemetry AI Coach

A local-first telemetry analysis platform for F1 simulation racing on PlayStation 5. The PS5 broadcasts UDP telemetry packets at up to 60 Hz; this application receives them, stores the data locally, runs AI specialist agents to analyse each completed lap, and surfaces ranked coaching feedback through a real-time Blazor dashboard — all on a single laptop with no cloud infrastructure required.

---

## Documentation

| Document | Description |
|---|---|
| [Architecture](./docs/architecture.md) | System design, layers, data flow, and component responsibilities |
| [Implementation Plan](./docs/implementation-plan.md) | Phased delivery plan with milestones |
| [.NET Guidelines](./docs/dotnet-guidelines.md) | Coding standards, patterns, and best practices |
| [Testing Strategy](./docs/testing-strategy.md) | Unit testing (70% coverage) and integration testing approach |

---

## Quick Start

### Prerequisites

- .NET 10 SDK
- Docker (for InfluxDB)
- An LLM backend — Ollama is recommended for fully local use

### 1. Start InfluxDB

```bash
docker compose up -d influxdb
```

### 2. Configure the LLM provider

Edit `src/F1Telemetry.App/appsettings.json`:

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

Valid providers: `ollama` · `openai` · `anthropic` · `lmstudio`

For Ollama, pull the model first: `ollama pull llama3.2`

### 3. Configure the PS5

In F1 25: **Settings → Telemetry → UDP Telemetry Output → On**

Set the IP address to your laptop's IP and the port to `20777`.

### 4. Run the application

```bash
dotnet run --project src/F1Telemetry.App
```

Open `http://localhost:5291`. Start a race on the PS5 and watch live telemetry stream in. After each lap, the AI coaching report appears automatically.

---

## Development Without a PS5

Enable replay mode to re-feed archived lap JSON through the full pipeline:

```json
// src/F1Telemetry.App/appsettings.Development.json
{
  "Storage": {
    "ReplayMode": true
  }
}
```

The `ReplayHostedService` enumerates `~/F1-Telemetry/sessions/` and publishes a `LapCompletedEvent` for each archived lap at 500 ms intervals, exercising the entire AI analysis pipeline without live UDP data.

---

## Architecture

```
PS5 (UDP 20777)
    │
UdpListenerService     ← BackgroundService, Channel<RawPacket> writer
    │
PacketParser           ← MemoryMarshal.Read, F1 25 struct layout (29-byte header)
    │
SessionManager         ← Lap boundary detection, publishes LapCompletedEvent
    │
    ├──► LapStorageService     ← InfluxDB (frame data) + SQLite (lap summaries)
    │
    └──► AgentOrchestrator     ← Task.WhenAll fan-out to 5 specialist agents
              │
              ├── DeltaAgent          sector time comparison
              ├── BrakingAgent        brake point analysis
              ├── CornerAgent         minimum speed & exit speed
              ├── TyreAgent           temperature & wear trends
              └── RacingLineAgent     lateral position vs. personal best
                        │
                  IChatClient (Polly retry, 3 attempts, exponential backoff)
                        │
                  CoachingReportReadyEvent
                        │
              TelemetryBroadcastService → SignalR → Blazor Dashboard
```

**Dependency chain:** `App → Agents → Storage → Ingestion → Contracts`

---

## Configuration Reference

| Key | Default | Description |
|-----|---------|-------------|
| `Udp:Port` | `20777` | UDP port matching the PS5 setting |
| `InfluxDb:Url` | `http://localhost:8086` | InfluxDB endpoint |
| `InfluxDb:Token` | `my-local-token` | InfluxDB auth token |
| `ConnectionStrings:Sqlite` | `~/F1-Telemetry/data/f1.db` | SQLite path |
| `Storage:ArchivePath` | `~/F1-Telemetry/sessions` | JSON archive root |
| `Storage:ReplayMode` | `false` | Enable replay from archive |
| `LLM:Provider` | `ollama` | `ollama` · `openai` · `anthropic` · `lmstudio` |
| `LLM:Model` | `gemma3` | Model name |
| `LLM:TimeoutSeconds` | `30` | Per-request LLM timeout |

---

## Testing

```bash
# Unit tests (no external dependencies)
dotnet test --filter "Category!=Integration"

# Integration tests (requires Docker + InfluxDB)
dotnet test --filter "Category=Integration"

# All tests with coverage report
dotnet test --collect:"XPlat Code Coverage"
```

### Benchmarks

```bash
dotnet run -c Release --project tests/F1Telemetry.Benchmarks
```

The `PacketParserBenchmarks` target is ≤ 50 µs per packet. Results are written to `BenchmarkDotNet.Artifacts/`.

---

## Health Check

```
GET /health
```

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "influxdb",     "status": "Healthy",  "description": "InfluxDB is reachable" },
    { "name": "udp_listener", "status": "Healthy",  "description": "UDP listener healthy — 14400 packets received, 0 dropped" }
  ]
}
```

## Packet Drop Metric

```
GET /api/metrics/packets
```

```json
{ "totalReceived": 14400, "totalDropped": 0, "dropRate": 0.0, "dropRatePercent": 0.0 }
```

The dashboard shows an orange warning banner when the drop rate exceeds 1%.

---

## Logs

- **Console** — `[HH:mm:ss LVL] SourceContext: Message`
- **Rolling file** — `~/f1telemetry/logs/app-YYYYMMDD.log` (7-day retention)

---

## Repository Structure

```
f1-telemetry-ai-coach/
├── src/
│   ├── F1Telemetry.App/           Blazor Server, SignalR hub, REST API, health checks
│   ├── F1Telemetry.Ingestion/     UDP listener, F1 25 packet parser, session manager
│   ├── F1Telemetry.Storage/       InfluxDB, SQLite EF Core, JSON archive, watchdog
│   ├── F1Telemetry.Agents/        AI agents, orchestrator, IChatClient providers + retry
│   └── F1Telemetry.Contracts/     Interfaces, domain records, events — no dependencies
├── tests/
│   ├── F1Telemetry.UnitTests/
│   ├── F1Telemetry.IntegrationTests/
│   └── F1Telemetry.Benchmarks/    BenchmarkDotNet — PacketParser < 50µs target
├── docs/
├── docker-compose.yml
└── README.md
```
