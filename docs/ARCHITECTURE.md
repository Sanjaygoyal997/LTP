# Curing press monitoring — architecture

Replaces the WinForms `curingApplication` wall display with a React front end over a
.NET service, keeping the same operator-facing semantics.

```
PLCs (Mitsubishi FX2N / Siemens S7)
   ↓
OPC server (Kepware KEPServerEX)
   ↓  IPressDataProvider
CuringMonitor.Api (.NET 8)
   ├─ PlantPollingService   reads every tag on a fixed cadence
   ├─ PressStatusEvaluator  tags → status, recipe, counters, totals
   ├─ PlantStateStore       latest snapshot
   ├─ REST   /api/layout, /api/snapshot, /api/presses/{id}, /health
   └─ SignalR /hubs/press-status  pushes each new snapshot
   ↓
React display (Vite + TypeScript)
```

## Why it is shaped this way

**One poll loop, many clients.** The service reads the plant once per cycle and pushes the
result to everyone connected. Extra browsers cost the plant network nothing, and every
screen shows the same instant — the legacy app opened its own OPC session per client.

**Status is decided server-side.** The evaluator owns the precedence rule
(no-comm → alarm → stop → run), so the wall display, a phone and any future report cannot
disagree about what a press is doing. The client only maps a status to a colour.

**Bad quality is explicit.** Providers return a reading for every requested tag; an
unreadable tag comes back bad rather than missing. A press that stops answering goes grey
instead of freezing on its last colour — the failure mode that matters most on a wall
display nobody is actively watching.

**Layout is data.** `plant-layout.json` holds trenches, rows and per-press tag addresses.
Re-arranging a bay or re-pointing a tag is a config change, not a code change, and
`LegacyConfigImporter` generates that file from the existing `config_AB.txt` so the shop
floor's tag map stays the single source of truth.

## Projects

| Path | Contents |
|---|---|
| `backend/src/CuringMonitor.Api` | ASP.NET Core service |
| `frontend` | React + TypeScript display |
| `docs/DATA-MAPPING.md` | every datapoint traced to its legacy source |
| `prototype/` | the original static HTML mock-up, kept for reference |

## Running it

```bash
# backend — http://localhost:5080, Swagger at /swagger
cd backend && dotnet run --project src/CuringMonitor.Api

# frontend — http://localhost:5173
cd frontend && npm install && npm run dev
```

The backend defaults to `Plant:Provider=simulated`, so both halves run with no plant
connection. Press **F** on the display for full screen.

Regenerate the layout from a legacy config:

```bash
cd backend
dotnet run --project src/CuringMonitor.Api -- import-legacy \
  /path/to/config_AB.txt src/CuringMonitor.Api/plant-layout.json "Curing Press Status"
```

## Connecting to the plant

Set `Plant:Provider` to `opc` and register an `IOpcSession` implementation in
`Program.cs`. That interface is the only place the OPC stack appears:

```csharp
public interface IOpcSession : IDisposable
{
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken ct);
    Task SubscribeAsync(IReadOnlyList<string> tags, CancellationToken ct);
    Task<IReadOnlyDictionary<string, TagValue>> ReadAsync(IReadOnlyList<string> tags, CancellationToken ct);
}
```

Two viable adapters:

* **OPC UA** — `OPCFoundation.NetStandard.Opc.Ua.Client`, against KEPServerEX's UA
  endpoint. Cross-platform, and the one to prefer for new work.
* **OPC DA (classic)** — the interop the legacy app used (`OPCAutomation`). Windows-only
  and requires DCOM configuration; use it only if the server exposes no UA endpoint.

`OpcPressDataProvider` already handles reconnect back-off and marks every tag bad while the
session is down, so an adapter only has to connect, subscribe and read.

## Configuration

| Setting | Default | Meaning |
|---|---|---|
| `Plant:PollInterval` | 2 s | how often the full tag set is read |
| `Plant:StaleAfter` | 30 s | no good reading for longer → `noCommunication` |
| `Plant:MinRunningPressure` | 1.0 | pressure at or above which a closed press is "running" |
| `Plant:Provider` | `simulated` | `simulated` or `opc` |
| `Plant:Shifts:*StartHour` | 7 / 15 / 23 | shift boundaries |
| `AllowedOrigins` | `http://localhost:5173` | CORS origins for the display |
