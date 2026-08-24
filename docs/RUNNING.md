# The interim build

What is on this branch today: the curing wall display running against the plant's
**existing** SCADA press configuration, with no conversion step and nothing to maintain
twice. The platform design in [ARCHITECTURE.md](ARCHITECTURE.md) is the target for when
there is time; this is what works now.

```bash
cd backend && dotnet run --project src/CuringMonitor.Api    # :5080, Swagger at /swagger
cd frontend && npm install && npm run dev                   # :5173
```

Defaults to the simulator, so both halves run with no plant connection. Press **F** on the
display for full screen.

## Pointing it at the plant's configuration

`Plant:LayoutFile` accepts the legacy file directly — a `.txt` path is parsed as the SCADA
press configuration, anything else as a layout file:

```json
"Plant": {
  "LayoutFile": "\\\\scada-u2\\SmartScada\\Projects\\PCRCuring\\config.txt",
  "Provider": "opc"
}
```

Everything comes from that file: press numbers, trench (bay) grouping, tile order, and the
three status tags, recipe tag and three shift-counter tags per press. Rows wrap at 16
tiles per trench and the trench pressure tile closes the last row, as on the existing
screen. The copy committed here is the `config_AB.txt` from the sample project — 86
presses across trenches 4, 5 and 6, 524 distinct tags.

The legacy file carries no trench pressure tag, so those are supplied from settings rather
than by editing a file the old system still reads:

```json
"TrenchPressureTags": { "4": "TRENCH.T4.pressure", "6": "TRENCH.T6.pressure" }
```

A trench with no tag shows as no-communication rather than inventing a value.

## What still has to happen before it runs on the shop floor

1. **Build it once.** The code has never been compiled — there was no .NET SDK available
   where it was written. Expect to clear a few errors on the first `dotnet build`.
2. **Write the OPC session.** `Plant:Provider=opc` needs an `IOpcSession` registered;
   without one the service fails at start-up with a message saying so. That is the only
   plant-facing code left, and it needs the site's answer on UA versus classic DA.
3. **Confirm the status thresholds.** `MinRunningPressure` is a guess at 1.0 kg/cm² and
   `StaleAfter` at 30 s; both are settings, but they should be checked against a press
   that is actually curing.
4. **Supply the full config.** The committed sample covers trenches 4–6 only.

## Settings

| Setting | Default | Meaning |
|---|---|---|
| `Plant:LayoutFile` | `config_AB.txt` | legacy `.txt` config, or a layout file |
| `Plant:Provider` | `simulated` | `simulated` or `opc` |
| `Plant:PollInterval` | 2 s | how often the full tag set is read |
| `Plant:StaleAfter` | 30 s | no good reading for longer → no-communication |
| `Plant:MinRunningPressure` | 1.0 | pressure at or above which a closed press is running |
| `Plant:TrenchPressureTags` | `{}` | trench number → header pressure tag |
| `Plant:Shifts:*StartHour` | 7 / 15 / 23 | shift boundaries |
| `AllowedOrigins` | `http://localhost:5173` | CORS origins for the display |

## API

| Endpoint | Purpose |
|---|---|
| `GET /api/layout` | tile grid: trenches, rows, cells |
| `GET /api/snapshot` | latest state of every press; 503 until the first poll lands |
| `GET /api/presses/{id}` | one press |
| `GET /health` | service and data-source state |
| `/hubs/press-status` | SignalR feed; snapshot on connect, then pushes |

`presses[].status` is one of `running`, `stopped`, `alarm`, `noCommunication`, and `shift`
is the letter `A`, `B` or `C`.

## Exporting the legacy config

If a site would rather edit a layout file than the legacy format:

```bash
dotnet run --project src/CuringMonitor.Api -- import-legacy \
  /path/to/config.txt src/CuringMonitor.Api/plant-layout.json "Curing Press Status"
```
