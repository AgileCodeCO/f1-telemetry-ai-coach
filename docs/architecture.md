# Architecture

## Overview

F1 Telemetry AI Coach is a four-layer local-first application. Every component runs on your development machine. There are no required cloud services — cloud LLM providers (OpenAI, Anthropic) are optional and swappable via configuration.

```
PS5 (F1 game)
    │  UDP broadcast · port 20777 · same LAN
    ▼
┌─────────────────────────────────────────────┐
│  Layer 1 — Ingestion                        │
│  .NET Worker Service                        │
│  UDP Listener → Packet Parser → Session Mgr │
└─────────────────┬───────────────────────────┘
                  │ Channel<TelemetryFrame>
    ┌─────────────┼──────────────┐
    ▼             ▼              ▼
┌──────────┐ ┌────────┐ ┌──────────────┐
│InfluxDB  │ │SQLite  │ │Local FS      │
│OSS       │ │        │ │~/f1telemetry/│
│:8086     │ │f1.db   │ │sessions/     │
└──────────┘ └────────┘ └──────────────┘
         Layer 2 — Storage

                  │ lap completed event
                  ▼
┌─────────────────────────────────────────────┐
│  Layer 3 — AI Agents                        │
│  Orchestrator → [Braking · Corner ·         │
│                  Tyre · Delta · Line]        │
│                  ↕                          │
│  IChatClient (Ollama / OpenAI / Anthropic)  │
└─────────────────┬───────────────────────────┘
                  │ SignalR push
                  ▼
┌─────────────────────────────────────────────┐
│  Layer 4 — Blazor Dashboard                 │
│  ASP.NET Core · localhost:5291              │
│  Live charts · AI coaching · Lap comparison │
└─────────────────────────────────────────────┘
```

---

## Layer 1 — Telemetry Ingestion

### UdpListenerService

A `BackgroundService` that owns a `UdpClient` bound to port 20777 and runs a continuous `ReceiveAsync` loop. Each datagram is forwarded as a raw `byte[]` to the `IPacketParser` without copying where possible (`Memory<byte>` / `ArrayPool<T>`).

**Responsibilities:**

- Bind to the configured UDP port on startup
- Receive datagrams asynchronously without blocking the thread pool
- Write raw bytes to a bounded `Channel<RawPacket>` (capacity 4096)
- Track receive statistics (packets/sec, dropped frames)
- Gracefully stop on `CancellationToken`

### PacketParser

Reads the 24-byte F1 packet header to determine `PacketId`, then uses `MemoryMarshal.Read<T>` to deserialize the remaining bytes into the appropriate strongly-typed struct. No heap allocations for parsing — all structs use `[StructLayout(LayoutKind.Sequential, Pack = 1)]`.

**Packet types handled:**

| PacketId | Struct | Contents |
|---|---|---|
| 0 | `PacketMotionData` | Car world position, velocity, G-forces |
| 1 | `PacketSessionData` | Track, weather, session type |
| 2 | `PacketLapData` | Lap time, sector splits, pit status |
| 6 | `PacketCarTelemetryData` | Speed, throttle, brake, gear, RPM, DRS, tyre temps |
| 7 | `PacketCarStatusData` | Tyre compound, fuel load, ERS mode |
| 10 | `PacketCarDamageData` | Tyre wear per wheel, damage levels |

### SessionManager

Consumes parsed frames from the channel, maintains the current session state, and detects lap boundaries. When a lap completes (rising edge on `lapNumber` from `PacketLapData`), it assembles a `CompletedLap` record from buffered frames and publishes it to the `IEventBus` for downstream consumers (storage writers and the AI agent pipeline).

---

## Layer 2 — Local Storage

Three complementary stores, each optimised for its access pattern.

### InfluxDB OSS (Time-Series)

Stores every `PacketCarTelemetryData` and `PacketMotionData` frame at full resolution. Tags enable efficient slice queries — "give me the throttle and brake trace for lap 14 between distance 800m and 1200m".

**Measurement schema:**

```
measurement: car_telemetry
tags:
  session_uid   (string)
  lap_number    (integer as string)
  track_id      (string)
  car_index     (string)
fields:
  speed_kmh         float
  throttle          float  [0.0–1.0]
  brake             float  [0.0–1.0]
  steering          float  [-1.0–1.0]
  gear              integer
  engine_rpm        float
  drs               boolean
  tyre_temp_fl      float
  tyre_temp_fr      float
  tyre_temp_rl      float
  tyre_temp_rr      float
timestamp: session_time (nanoseconds)
```

### SQLite (Relational)

Stores structured summaries queried by the AI agents for trend analysis across sessions.

**Entity model:**

```sql
-- Sessions
CREATE TABLE Sessions (
    Id          TEXT PRIMARY KEY,   -- sessionUID as hex string
    TrackName   TEXT NOT NULL,
    SessionType TEXT NOT NULL,      -- Race, Time Trial, Practice
    StartedAt   TEXT NOT NULL,      -- ISO 8601
    TyreCompound TEXT
);

-- Laps
CREATE TABLE Laps (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    SessionId       TEXT NOT NULL REFERENCES Sessions(Id),
    LapNumber       INTEGER NOT NULL,
    LapTimeMs       INTEGER,        -- NULL if in-lap/invalid
    Sector1Ms       INTEGER,
    Sector2Ms       INTEGER,
    Sector3Ms       INTEGER,
    IsPersonalBest  INTEGER NOT NULL DEFAULT 0,
    IsValid         INTEGER NOT NULL DEFAULT 1,
    MaxSpeedKmh     REAL,
    AvgThrottle     REAL,
    TyreWearFl      REAL,
    TyreWearFr      REAL,
    TyreWearRl      REAL,
    TyreWearRr      REAL
);

-- AI feedback per lap
CREATE TABLE LapFeedback (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    LapId       INTEGER NOT NULL REFERENCES Laps(Id),
    AgentName   TEXT NOT NULL,
    Category    TEXT NOT NULL,      -- Braking, Corner, Tyre, Delta, Line
    Finding     TEXT NOT NULL,
    EstimatedGainMs INTEGER,        -- potential time gain in milliseconds
    GeneratedAt TEXT NOT NULL
);
```

### Local Filesystem (Archive)

Each completed lap is serialized to JSON and written to `~/f1telemetry/sessions/{sessionUID}/lap_{n:D2}.json`. This serves as a replay source for the integration test harness and as a long-term archive that can be re-processed through updated agents without re-driving.

---

## Layer 3 — AI Agents

All agents are stateless — context is injected per invocation via structured prompts.

### IChatClient — Pluggable Backend

The LLM abstraction is `Microsoft.Extensions.AI.IChatClient` (package `Microsoft.Extensions.AI` 10.5.0), the standard .NET interface for chat-completion providers. Each agent calls `GetResponseAsync(messages, options, ct)` and reads `ChatResponse.Text` — no agent is coupled to a specific LLM vendor.

Implementations are selected by the `LLM.Provider` configuration key:

| Provider value | Implementation | Notes |
|---|---|---|
| `ollama` | `OllamaApiClient` (OllamaSharp 5.4.25) | Native Ollama client; primary local LLM option |
| `lmstudio` | `OpenAI.Chat.ChatClient.AsIChatClient()` | OpenAI-compatible endpoint; set `BaseUrl` to `http://localhost:1234` |
| `openai` | `OpenAI.Chat.ChatClient.AsIChatClient()` | Official OpenAI SDK via `Microsoft.Extensions.AI.OpenAI` |
| `anthropic` | `AnthropicChatClient` (internal) | Thin `IChatClient` wrapper over the Anthropic HTTP API; no official adapter exists yet |

### Agent Specialists

Each specialist receives a `LapAnalysisContext` containing the relevant telemetry slice, track metadata, and historical averages, then returns a `AgentFinding` with a natural-language explanation and an estimated lap time delta in milliseconds.

**BrakingAgent** — detects suboptimal brake application. Compares the distance from corner apex to brake marker. Flags: brake too early (leaving speed on the table), brake too late (running wide), not enough trail braking into apex, or brake release too slow on exit.

**CornerAgent** — analyzes minimum corner speed, throttle application point, and exit speed. The largest gains are typically found at medium-speed corners where throttle application timing has an outsized effect on exit speed and the following straight.

**TyreAgent** — correlates tyre temperature and wear rate with driving behaviour. Overheating front-left in slow corners indicates aggressive turn-in. Consistent rear wear asymmetry suggests a setup issue or steering bias.

**DeltaAgent** — time-distance comparison between the current lap and the personal best. Produces a per-sector breakdown: "you lost 0.31s in the T4–T6 complex, gained 0.08s on the main straight." This is the most immediately actionable output.

**RacingLineAgent** — uses `PacketMotionData` world coordinates to reconstruct the driven line and compare it geometrically to the theoretical ideal for each corner. Flags wide entries, late apexes, or exits that compromise the following straight.

### Orchestrator

Receives the `CompletedLap` event, fans out to all five specialists concurrently using `Task.WhenAll`, then synthesizes findings into a ranked `LapCoachingReport`. Findings are ordered by `EstimatedGainMs` descending so the pilot sees the highest-value changes first.

---

## Layer 4 — Blazor Dashboard

An ASP.NET Core application hosting both the Blazor Server frontend and the REST/SignalR API. Runs entirely on `localhost:5291` — no separate frontend build step required.

### Pages

**Dashboard (/)** — live telemetry strip showing speed, throttle, brake, and gear traces for the current lap in real time, synchronized to track distance rather than time.

**Lap Review (/lap/{id})** — full post-lap analysis. Shows the AI coaching report with ranked findings, the speed/input trace for the reviewed lap, and a distance-aligned overlay comparing it to the personal best.

**Session History (/sessions)** — table of all recorded sessions with lap time trends, best sector combination, and session-level consistency score.

**Settings (/settings)** — LLM provider selector, UDP port configuration, and data management (export, purge).

### Real-Time Updates

A SignalR hub (`/hubs/telemetry`) pushes updates to connected browsers:

- `ReceiveTelemetryFrame` — fired at ~10Hz (throttled from 60Hz) for the live trace
- `LapCompleted` — fires when a new lap is detected, carries the lap summary
- `CoachingReportReady` — fires when the AI orchestrator has finished analysis, carries the full `LapCoachingReport`

---

## Infrastructure

### docker-compose.yml

```yaml
services:
  influxdb:
    image: influxdb:2.7
    ports:
      - "8086:8086"
    environment:
      DOCKER_INFLUXDB_INIT_MODE: setup
      DOCKER_INFLUXDB_INIT_USERNAME: f1admin
      DOCKER_INFLUXDB_INIT_PASSWORD: f1password
      DOCKER_INFLUXDB_INIT_ORG: f1telemetry
      DOCKER_INFLUXDB_INIT_BUCKET: telemetry
      DOCKER_INFLUXDB_INIT_ADMIN_TOKEN: my-local-token
    volumes:
      - ./data/influxdb:/var/lib/influxdb2
```

The main application and SQLite run natively (not in Docker) so that the UDP socket can bind directly to the host network adapter without NAT complications.

### Configuration

`appsettings.json` defines all runtime knobs:

```json
{
  "Udp": {
    "Port": 20777,
    "BufferSize": 4096
  },
  "InfluxDb": {
    "Url": "http://localhost:8086",
    "Token": "my-local-token",
    "Org": "f1telemetry",
    "Bucket": "telemetry"
  },
  "ConnectionStrings": {
    "Sqlite": "Data Source=./data/f1.db"
  },
  "LLM": {
    "Provider": "ollama",
    "Model": "llama3.2",
    "BaseUrl": "http://localhost:11434",
    "TimeoutSeconds": 30
  },
  "Storage": {
    "ArchivePath": "~/f1telemetry/sessions"
  }
}
```
