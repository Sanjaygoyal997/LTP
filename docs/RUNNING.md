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

## Screens are configuration

What the display draws comes from a screen document in `backend/src/CuringMonitor.Api/screens/`,
served verbatim at `/api/screens/{id}`. The service validates only the envelope — an id, a
title and widgets that each name a type — and passes widget contents through untouched, so
changing a screen never means changing the backend.

```json
{
  "id": "curing-wall",
  "title": "Curing Press Status",
  "theme": {
    "floor": "#d4d4d4",
    "status": { "running": "#00e05a", "stopped": "#ffe400",
                "alarm": "#ff1e1e", "noCommunication": "#9a9a9a" },
    "tile": { "minWidth": 52, "maxWidth": 112 },
    "alarmPulse": true
  },
  "widgets": [
    { "type": "tile-grid", "region": "floor",
      "tile": { "header": "asset.title", "sub": "signal.recipeCode",
                "value": "signal.count", "colour": "status" } },
    { "type": "kpi-panel", "region": "footer", "title": "Production",
      "items": [ { "label": "A", "field": "production.a" } ] },
    { "type": "legend", "region": "footer", "title": "Legends" }
  ]
}
```

**Fields** are dotted paths named after what an engineer thinks in, not after the JSON the
API happens to send:

| Root | Resolves to | Example |
|---|---|---|
| `asset.*` | identity of the press | `asset.title` |
| `signal.*` | live values | `signal.recipeCode`, `signal.count`, `signal.pressure` |
| `status` / `status.label` | state, or its display text | `status.label` |
| `trench.*` | trench header values | `trench.pressure` |
| `production.*` | per-shift production | `production.a`, `production.total` |
| `totals.*` | press counts by state | `totals.running`, `totals.stopped` |

Point a tile's `value` at `signal.pressure` instead of `signal.count` and the wall shows
live pressure; change `theme.status.running` and the running colour changes. Neither is a
code change.

**Widget types** available today are `tile-grid`, `kpi-panel` and `legend`. A new type is a
React component plus one entry in the registry — again, no backend change. An unknown type
renders as a visible error on the screen rather than silently disappearing, so a typo in
the config is obvious.

**Several screens.** Drop more documents in the directory and point a panel at
`?screen=<id>`. `default` serves the first screen alphabetically, so a wall panel can be
configured once and never revisited.

**Live editing.** With `Plant:WatchScreens` true (the default in development) the service
watches the directory and pushes a change signal over SignalR; every open display re-fetches
its screen and re-renders. Save the file, watch the wall change. A malformed file is logged
and skipped — the previous catalogue keeps serving, so a bad edit cannot blank the screen.

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
