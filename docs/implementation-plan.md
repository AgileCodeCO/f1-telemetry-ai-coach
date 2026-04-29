# Implementation Plan

## Principles

- Each sprint delivers a working, testable vertical slice — never infrastructure without a user-visible outcome
- The UDP-to-storage path is validated before any AI layer is built
- The Delta agent ships before the other specialists — it has the clearest success criteria and the highest immediate value to the pilot
- Every sprint includes its own unit and integration tests; testing is never deferred to a later sprint

---

## Sprint 0 — Project Scaffold (1–2 days)

**Goal:** Repository structure, build pipeline, and local tooling verified.

### Tasks

- Create the solution with five projects: `F1Telemetry.App`, `F1Telemetry.Ingestion`, `F1Telemetry.Storage`, `F1Telemetry.Agents`, `F1Telemetry.Contracts`
- Add test projects: `F1Telemetry.UnitTests`, `F1Telemetry.IntegrationTests`
- Configure `docker-compose.yml` for InfluxDB
- Add `appsettings.json` with all configuration keys stubbed
- Set up `EditorConfig` and `global.json` pinning the .NET SDK version
- Add `Directory.Build.props` with shared NuGet package versions
- Add a basic GitHub Actions (or local Makefile) pipeline: `restore → build → test`
- Verify Docker + InfluxDB start cleanly

### Deliverable

`dotnet build` and `dotnet test` pass. InfluxDB is reachable at `localhost:8086`.

---

## Sprint 1 — UDP Ingestion + Packet Parsing (3–4 days)

**Goal:** Receive live telemetry from the PS5 and log parsed packet fields to the console.

### Tasks

**F1Telemetry.Contracts**

- Define packet header struct: `PacketHeader`
- Define enums: `PacketId`, `SessionType`, `TyreCompound`
- Define core domain models: `RawPacket`, `TelemetryFrame`, `LapSnapshot`
- Define `IPacketParser` and `ISessionManager` interfaces

**F1Telemetry.Ingestion**

- Implement `UdpListenerService : BackgroundService`
  - `UdpClient` bind, async receive loop, `Channel<RawPacket>` write
  - Configurable port, buffer pool via `ArrayPool<byte>`
- Implement `PacketParser : IPacketParser`
  - `MemoryMarshal.Read<T>` deserialization per packet ID
  - All six packet structs with `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  - Structured logging of unknown/malformed packets
- Implement `SessionManager : ISessionManager`
  - Lap boundary detection from `PacketLapData.currentLapNum`
  - Publishes `LapCompletedEvent` on the in-process event bus
- Register services in `IServiceCollection` extension: `AddIngestion()`

**Tests (Sprint 1)**

- Unit: `PacketParser` — deserialize fixture bytes for each packet ID, assert field values
- Unit: `SessionManager` — simulate lap number transitions, assert event published
- Unit: `UdpListenerService` — mock channel, assert write-on-receive, assert graceful stop
- Integration: `UdpIngestionPipelineTests` — send real UDP datagrams from the test harness, assert parsed frames arrive downstream (see Testing Strategy)

### Deliverable

With the PS5 on the same network (or the integration test harness running), the console shows a stream of parsed telemetry fields. The lap-completed event fires and is logged.

---

## Sprint 2 — Local Storage (3–4 days)

**Goal:** Persist telemetry to InfluxDB and SQLite. Verify data round-trips correctly.

### Tasks

**F1Telemetry.Storage**

- Implement `InfluxTelemetryRepository : ITelemetryRepository`
  - Write `PacketCarTelemetryData` frames as line protocol points
  - Query methods: `GetLapTraceAsync(sessionId, lapNumber)`, `GetSectorTraceAsync(...)`
  - Use the official `InfluxDB.Client` NuGet package
- Implement `SqliteLapRepository : ILapRepository` (EF Core)
  - Entities: `SessionEntity`, `LapEntity`, `LapFeedbackEntity`
  - Migrations via `dotnet ef migrations add`
  - Repository methods: `SaveLapAsync`, `GetLapsBySessionAsync`, `GetPersonalBestAsync`
- Implement `FileLapArchive : ILapArchive`
  - Write completed lap JSON to `{ArchivePath}/{sessionUID}/lap_{n:D2}.json`
  - Read back for replay: `GetArchivedLapAsync(sessionUID, lapNumber)`
- Register services: `AddStorage()`

**Tests (Sprint 2)**

- Unit: `SqliteLapRepository` — use SQLite in-memory mode, assert CRUD operations
- Unit: `FileLapArchive` — use a temp directory, assert write/read round-trip
- Integration: `InfluxTelemetryRepositoryTests` — write sample frames to a local InfluxDB (started via Docker in CI), query back and assert values match
- Integration: `StoragePipelineTests` — run ingestion + storage together using the UDP test harness, assert a completed lap appears in both SQLite and InfluxDB

### Deliverable

After a simulated 3-lap session from the test harness, all lap data is queryable in InfluxDB and SQLite. JSON files are written to the archive folder.

---

## Sprint 3 — Delta Agent + LLM Integration (4–5 days)

**Goal:** First AI coaching feedback. After each lap, generate a time-delta breakdown and push it to the console (no UI yet).

### Tasks

**F1Telemetry.Agents**

- Define `ILlmClient` interface with a single `CompleteAsync` method
- Implement `OllamaLlmClient` (default, no API key needed)
- Implement `OpenAiLlmClient` and `AnthropicLlmClient` (keyed registrations)
- Implement `LlmClientFactory` — reads `LLM.Provider` from config, returns the right `ILlmClient`
- Define `AgentFinding`, `LapCoachingReport`, `LapAnalysisContext`
- Implement `DeltaAgent`
  - Fetches personal best lap from `ILapRepository`
  - Fetches time-distance traces for both laps from `ITelemetryRepository`
  - Builds a structured prompt with numeric context (not raw CSV — summarize per sector)
  - Calls `ILlmClient.CompleteAsync`, parses the response into `AgentFinding`
- Implement `AgentOrchestrator` (stub for other agents, wires only Delta for this sprint)
- Subscribe `AgentOrchestrator` to `LapCompletedEvent`
- Register services: `AddAgents()`

**Tests (Sprint 3)**

- Unit: `DeltaAgent` — mock `ILapRepository`, mock `ITelemetryRepository`, mock `ILlmClient` returning fixture JSON; assert `AgentFinding` parsed correctly
- Unit: `LlmClientFactory` — assert correct implementation resolved for each provider name
- Unit: `AgentOrchestrator` — mock all agents, assert fan-out and result merge
- Integration: `DeltaAgentIntegrationTests` — run with a real SQLite (in-memory) seeded with two laps; mock `ILlmClient`; assert the prompt contains the expected sector delta values

### Deliverable

After each simulated lap from the test harness, a formatted delta report prints to the console within a few seconds.

---

## Sprint 4 — Remaining Agents (4–5 days)

**Goal:** Full specialist set producing ranked coaching feedback.

### Tasks

**F1Telemetry.Agents — Specialists**

- Implement `BrakingAgent`
  - Identifies brake points per corner from the telemetry trace (distance where brake > 0.1)
  - Compares to reference from personal best; flags corners with >5m deviation
  - Detects incomplete trail braking (brake drops to zero before minimum corner speed achieved)
- Implement `CornerAgent`
  - Extracts minimum speed per corner segment
  - Identifies throttle application point (distance where throttle > 0.1 after corner entry)
  - Compares exit speeds on the following 200m straight
- Implement `TyreAgent`
  - Reads tyre temps and wear from `PacketCarStatusData` and `PacketCarDamageData`
  - Detects overheating (temp > threshold for compound), asymmetric wear
  - Cross-references with session lap count to identify degradation trend
- Implement `RacingLineAgent`
  - Uses world XZ coordinates from `PacketMotionData` to reconstruct the driven line
  - Computes lateral offset from personal best line per corner segment
  - Flags entry and exit deviations above a configurable threshold (default 1.5m)
- Wire all five agents into `AgentOrchestrator.RunAllAsync` with `Task.WhenAll`
- Sort findings by `EstimatedGainMs` descending before returning the report

**Tests (Sprint 4)**

- Unit tests for each new agent following the same mock-inject pattern as `DeltaAgent`
- Unit: `AgentOrchestrator` — assert full fan-out, assert findings sorted correctly
- Integration: Full agent pipeline with in-memory SQLite + fixture telemetry JSON; assert report contains findings from all five agents

### Deliverable

A complete `LapCoachingReport` with ranked findings from all specialists is produced after each lap.

---

## Sprint 5 — Blazor Dashboard (5–6 days)

**Goal:** Visible, real-time coaching interface. The dashboard is the primary deliverable of the project.

### Tasks

**F1Telemetry.App**

- Add SignalR hub: `TelemetryHub`
  - Push `TelemetryFrameDto` at 10Hz (decimated from 60Hz)
  - Push `LapCompletedDto` on lap boundary
  - Push `CoachingReportDto` when orchestrator finishes
- Add REST endpoints: `GET /api/sessions`, `GET /api/sessions/{id}/laps`, `GET /api/laps/{id}/feedback`
- Implement Blazor pages:
  - `/` — live telemetry strip (speed, throttle/brake overlay, gear). Uses `IJSRuntime` for Chart.js via JS interop
  - `/lap/{id}` — lap review with AI coaching cards ranked by gain, speed trace, lap overlay
  - `/sessions` — session history table with lap time trend sparklines
  - `/settings` — LLM provider form, UDP port config, archive path display
- Add `TelemetryState` service (`scoped`) as the in-memory state container for the live page
- Implement `LapChartComponent` and `CoachingReportComponent` as reusable Blazor components

**Tests (Sprint 5)**

- Unit: `TelemetryState` — assert frame buffering, decimation to 10Hz, rolling window trim
- Unit: Coaching card sort logic — assert findings ordered by gain
- Integration: `SignalRHubTests` — using `WebApplicationFactory<Program>`, connect a test client to the hub, fire a simulated lap event, assert the client receives `CoachingReportReady` with the expected payload
- Integration: REST API tests with `WebApplicationFactory` — assert session/lap endpoints return correctly shaped responses

### Deliverable

The full application is usable end-to-end. Open `http://localhost:5000`, start a race on the PS5, and see live telemetry. After each lap, the AI coaching report appears automatically.

---

## Sprint 6 — Polish, Hardening, and Performance (3–4 days)

**Goal:** Production-quality reliability for everyday use.

### Tasks

- Add structured logging (`Serilog`) to console + rolling file (`~/f1telemetry/logs/`)
- Add health check endpoint (`/health`) reporting InfluxDB connectivity and UDP listener status
- Add `IHostedService` watchdog that reconnects InfluxDB on transient failure
- Implement LLM retry policy with exponential backoff (Polly)
- Add `PacketDropCounter` metric (via `System.Diagnostics.Metrics`) — alert in the UI if drop rate exceeds 1%
- Performance: benchmark `PacketParser` with BenchmarkDotNet, assert < 50µs per packet
- Add `appsettings.Development.json` with a replay mode flag that feeds archived JSON instead of live UDP — removes the PS5 dependency for development
- Write the complete README and finalize all docs

### Deliverable

The application handles 60Hz telemetry for a full race distance without memory growth or dropped frames. Logs provide clear diagnostics when the LLM is slow or unavailable.

---

## Milestones Summary

| Sprint | Duration | Key Deliverable |
|---|---|---|
| 0 | 2 days | Solution scaffold, CI pipeline |
| 1 | 4 days | Live UDP parsing to console |
| 2 | 4 days | Full data persistence |
| 3 | 5 days | First AI coaching output (Delta agent) |
| 4 | 5 days | All five specialist agents |
| 5 | 6 days | Blazor dashboard, real-time updates |
| 6 | 4 days | Reliability, logging, performance |
| **Total** | **~30 days** | |
