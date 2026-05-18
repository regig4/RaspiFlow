# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**RaspiFlow** is an IoT platform that connects Raspberry Pi sensors to Azure cloud services. It ingests telemetry via EventHub and ServiceBus, runs AI anomaly detection, and features a multi-agent image recognition pipeline backed by a SQL Server vector store.

## Build & Run

```bash
# Run the full local environment (all services + emulators)
cd RaspberryAzure.AppHost && dotnet run

# Build the entire solution
dotnet build RaspberryAzure.sln

# Run F# unit tests (AnomalyDetector)
dotnet test RaspberryAzure.AnomalyDetector/RaspberryAzure.AnomalyDetector.fsproj

# Run Playwright E2E tests (requires services running)
npx playwright test

# React frontend dev server (standalone)
cd RaspberryAzure.ReactClient && npm run dev

# Python sensor simulator (requires AppHost running first)
cd PythonClient && python start.py
```

The AppHost (`dotnet run` from `RaspberryAzure.AppHost`) is the primary entry point for local development — it orchestrates all services and spins up Azure emulators via Docker containers.

## Architecture

### Data Flow

```
PythonClient (sensor simulator)
  ├── EventHub → PersistanceWorker (EventHubReaderFunc) → Azure Blob Storage
  └── ServiceBus → PersistanceWorker (QueueReaderFunc) → CosmosDB (disabled)
                 → AggregatorService (batches 10 records) → AgentAnalyzer /addData
                                                                    ↓
                                                    ReactClient → AgentAnalyzer /askAi
                                                                    ↓ SK + GPT-4.1
                                                               AnomalyDetector (F#)
```

Image recognition is an independent pipeline:
```
POST /label-image → pixel diff filter → Ollama 3-agent workflow → embedding similarity filter → SQL Server (SceneDb)
POST /query       → vector search (cosine) → Ollama generation → answer with sources
```

### Projects

| Project | Type | Role |
|---|---|---|
| `RaspberryAzure.AppHost` | .NET Aspire host | Orchestrates all services and Azure emulators locally |
| `RaspberryAzure.PersistanceWorker` | Azure Functions v4 | Reads EventHub → Blob; reads ServiceBus → CosmosDB |
| `RaspberryAzure.AggregatorService` | Background worker | Consumes ServiceBus, batches 10 records, forwards to AgentAnalyzer |
| `RaspberryAzure.AgentAnalyzer` | ASP.NET Web API | Semantic Kernel agent (GPT-4.1) with stats tools; also exposes an MCP server over stdio |
| `RaspberryAzure.AnomalyDetector` | F# library + xunit | 2-sigma anomaly detection; consumed by AgentAnalyzer via interop |
| `RaspberryAzure.ImageRecognition` | ASP.NET Web API | Multi-agent image labeling pipeline; vector search over snapshots |
| `RaspberryAzure.ServiceDefaults` | Shared library | Common Aspire/OpenTelemetry configuration |
| `RaspberryAzure.ReactClient` | React + Vite (TypeScript) | Chat UI forwarding prompts to AgentAnalyzer's `/askAi` |
| `PythonClient` | Python script | Simulates Raspberry Pi: sends events to EventHub and ServiceBus |

### Key Design Points

**AgentAnalyzer** uses Semantic Kernel with `FunctionChoiceBehavior.Auto()` so the LLM can call `BasicStatsPlugin` functions (`get_avg`, `get_min`, `get_max`, `get_anomalies`, `run_nn`). The plugin calls the F# `getAnomalies` function directly via .NET interop and can invoke a Python neural network via CSnakes. The service also registers itself as an MCP server over stdio, exposing the same kernel functions as MCP tools.

**ImageRecognition** has a two-stage deduplication guard before running the expensive LLM pipeline:
1. Pixel diff (SkiaSharp, 64×64 grayscale) — skips if < 5% pixels changed
2. Cosine similarity on `nomic-embed-text` embeddings — skips if > 0.95 similarity to last snapshot

The `WorkflowFactory` builds a sequential 3-agent workflow (DescriptionAgent → TagsAgent → LabelAgent) using Ollama's `llava` model via `Microsoft.Agents.AI.Workflows`.

**AggregatorService** uses a bounded `Channel<Record>` (capacity 10) — it only forwards a batch when the channel is full, so messages accumulate until a full batch is ready.

## Configuration Requirements

**AgentAnalyzer** requires an OpenAI API key. Set it via user secrets:
```bash
cd RaspberryAzure.AgentAnalyzer
dotnet user-secrets set "OpenApiKey" "<your-key>"
```
Or update `appsettings.json` (not committed).

**ImageRecognition** requires Ollama running at `http://localhost:11434` with two models pulled:
```bash
ollama pull llava           # used for image description/tagging/labeling
ollama pull nomic-embed-text  # used for semantic embeddings
```

**AppHost** uses local emulators for all Azure services (EventHub, ServiceBus, Blob Storage, SQL Server via Docker). No real Azure credentials are needed for local development.

## Testing

- **F# unit tests** live in `RaspberryAzure.AnomalyDetector/Tests.fs` and use xunit.
- **Playwright E2E tests** live in `tests/basic.spec.ts` and hit the AgentAnalyzer's `/askAi` endpoint at `http://localhost:5029`.
- There are `.http` files (`*.http`) in several projects for manual HTTP testing.
