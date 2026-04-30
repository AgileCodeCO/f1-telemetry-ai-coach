# F1 Telemetry AI Coach

A local-first telemetry analysis platform for F1 simulation racing on PlayStation 5. The application receives live UDP telemetry from the F1 game, stores it locally, and uses AI agents to generate actionable lap-by-lap coaching feedback — all running on your laptop with no cloud infrastructure required.

---

## Documentation

| Document | Description |
|---|---|
| [Architecture](./docs/architecture.md) | System design, layers, data flow, and component responsibilities |
| [Implementation Plan](./docs/implementation-plan.md) | Phased delivery plan with sprint breakdown and milestones |
| [.NET Guidelines](./docs/dotnet-guidelines.md) | Coding standards, patterns, and best practices for this project |
| [Testing Strategy](./docs/testing-strategy.md) | Unit testing (70% coverage) and integration testing approach |

---

## Quick Start

### Prerequisites

- .NET 10 SDK
- Docker Desktop (for InfluxDB)
- Ollama (optional, for local LLM)
- F1 24 or F1 25 on PS5, configured to broadcast UDP to your laptop's IP on port 20777

### Run locally

```bash
# Start InfluxDB
docker compose up -d influxdb

# Run the application
dotnet run --project src/F1Telemetry.App

# Open dashboard
start http://localhost:5291
```

### Configure the LLM backend

Edit `appsettings.json` and set `LLM.Provider` to one of: `ollama`, `openai`, `anthropic`, or `lmstudio`.

```json
{
  "LLM": {
    "Provider": "ollama",
    "Model": "llama3.2",
    "BaseUrl": "http://localhost:11434"
  }
}
```

---

## Repository Structure

```
f1-telemetry/
├── src/
│   ├── F1Telemetry.App/          # Blazor frontend + ASP.NET Core host
│   ├── F1Telemetry.Ingestion/    # UDP listener and packet parser
│   ├── F1Telemetry.Storage/      # InfluxDB, SQLite, and file repositories
│   ├── F1Telemetry.Agents/       # AI agents and orchestrator
│   └── F1Telemetry.Contracts/    # Shared interfaces, DTOs, domain models
├── tests/
│   ├── F1Telemetry.UnitTests/
│   └── F1Telemetry.IntegrationTests/
├── docs/
│   ├── architecture.md
│   ├── implementation-plan.md
│   ├── dotnet-guidelines.md
│   └── testing-strategy.md
├── docker-compose.yml
└── README.md
```
