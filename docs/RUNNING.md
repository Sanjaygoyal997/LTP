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

Everything comes from that file: box names, group (trench) membership, order within the
group, and the three status tags, recipe tag and three shift-counter tags per press. The
box caption is `PressTitle` (column 2), not `PressName` — the two differ in practice.

The copy committed here is the plant's own file: **137 boxes across trenches 6, 5, 4, 2, 1
and 7**, in that order. Note what it contains, because it shapes the loader:

* `T_6`, `T_5`, `T_4`, `TRH`, `T_2`, `T_1` are ordinary entries with their own tags — the
  trench summary boxes are configured, not synthesised.
* Trench 7 holds `NewChina` and `OldChina`, which are painting-line weight checks rather
  than presses.
* Press `9201` appears twice, in two different trenches, so a box identifier carries its
  trench (`6/9201`) and the loader does not treat a repeated name as an error.
* Trench 3 does not exist.

**`trenchSize.txt`**, if the site ships it beside the config, is read too. The legacy
screen never stated a boxes-per-row figure — it sized each trench panel in pixels and
fitted the boxes into it, so the panel dimensions are what the client works from:

```
areaPerBox = panelWidth * panelHeight * 0.8 / boxCount
boxHeight  = round(sqrt(areaPerBox / 1.225))      # 1.225 is the mimic's box aspect
boxWidth   = round(boxHeight * 1.225)
shrink boxWidth until floor(w/boxWidth) * floor(h/boxHeight) >= boxCount
boxesPerRow = floor(panelWidth / boxWidth)
```

Format is a header line (`id,w,h`), then one comma-separated line per trench **in the same
order the trenches appear in the press configuration** — the `id` column numbers that
sequence, it is not the trench number. Trenches beyond the end of the file fall back to the
screen's own row width.

Group **order** comes from the configuration too — the sequence the plant lists its trenches
in, which is not necessarily alphabetical and is the one operators know.

Sites that would rather edit a plain asset file than the legacy format can use one: any
non-`.txt` path is read as JSON, with each asset carrying `id`, `kind`, `label`, `group`,
`position`, `attributes` and `signals` (signal name to tag address), and an optional
`groups` list carrying each group's `order` and `wrap`.

The legacy file carries no trench pressure tag, so those are supplied from settings rather
than by editing a file the old system still reads:

```json
"GaugeTags": { "Trench 4": "TRENCH.T4.pressure", "Trench 6": "TRENCH.T6.pressure" }
```

A gauge with no tag shows as no-communication rather than inventing a value.

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

### Two axes of configuration

**Look and feel** is the `theme` block and the per-widget specs — colours, density, which
field each line of a box shows.

**Which boxes exist** is data. The service publishes every box as an *asset* carrying its
own label, group, position, free-form attributes and signal values. A screen selects boxes
with a query rather than listing them:

```json
"source": {
  "where":  { "asset.attributes.trench": "6" },
  "groupBy": "asset.group",
  "orderBy": "asset.position",
  "wrap": "auto",
  "wrapByGroup": { "Trench 4": 12 }
}
```

`wrap` is `"auto"` to follow the width the plant configuration gives each group, or a
number to apply one width everywhere. `wrapByGroup` overrides both.

Commission a press into a group and its box appears; rename it in the plant configuration
and the box is renamed; regroup it and it moves. None of that touches a screen document or
a line of code. `where` matches on equality, and a list means "any of" — deliberately not
an expression language, so a typo fails visibly instead of silently matching everything.

Boxes have a `kind`. `press` is evaluated against the curing status rules; `gauge` just
shows its `value` signal — that is what the trench header-pressure boxes are. `tileByKind`
gives each kind its own field bindings inside the same grid.

**Fields** are dotted paths named after what an engineer thinks in, not after the JSON the
API happens to send:

| Root | Resolves to | Example |
|---|---|---|
| `asset.*` | identity and placement | `asset.label`, `asset.group`, `asset.position` |
| `asset.attributes.*` | whatever the plant configuration carries | `asset.attributes.trench` |
| `signal.*` | any signal wired up for that box | `signal.recipe`, `signal.count`, `signal.pressure` |
| `status` / `status.label` | state, or its display text | `status.label` |
| `production.*` | per-shift production | `production.a`, `production.total` |
| `totals.*` | press counts by state | `totals.running`, `totals.stopped` |

Signal names are the plant's vocabulary, not a fixed list: whatever the configuration binds
to a tag is published under that name and can be shown on a box. `count` is published as
well, resolving to whichever shift counter is currently running.

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

**Live editing.** Both configuration files are watched, so an edit reaches the wall
without restarting anything:

| Edit | How it reaches the display | Setting |
|---|---|---|
| A screen document | the service pushes a change signal over SignalR; every open display re-fetches and re-renders | `Plant:WatchScreens` |
| The plant configuration (`config_AB.txt`) | reloaded in place; the next poll publishes the new set of boxes, so a renamed press is renamed and a commissioned one appears | `Plant:WatchConfiguration` |

Both default to on. A malformed or half-saved file is logged and skipped — the previous
configuration keeps serving, so a bad edit cannot blank the screen. A reload that changes
the tag set also resubscribes the OPC session, so newly commissioned boxes get values
rather than sitting grey.

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
| `Plant:GaugeTags` | `{}` | group name → gauge tag |
| `Plant:LegacyTilePitch` | 46 | box width in pixels, for converting `trenchSize.txt` widths into boxes per row |
| `Plant:Shifts:*StartHour` | 7 / 15 / 23 | shift boundaries |
| `AllowedOrigins` | `http://localhost:5173` | CORS origins for the display |

## API

| Endpoint | Purpose |
|---|---|
| `GET /api/snapshot` | every box: label, group, position, status, attributes, signals; 503 until the first poll lands |
| `GET /api/assets/{id}` | one box |
| `GET /api/screens` | screens this service serves |
| `GET /api/screens/{id}` | one screen document, verbatim (`default` for the first) |
| `GET /health` | service and data-source state |
| `/hubs/press-status` | SignalR feed; snapshot on connect, then pushes |

`assets[].status` is one of `running`, `stopped`, `alarm`, `noCommunication`, and `shift`
is the letter `A`, `B` or `C`. There is no layout endpoint: the boxes and their arrangement
come from the assets themselves plus the screen's query.

## Exporting the legacy config

If a site would rather edit a layout file than the legacy format:

```bash
dotnet run --project src/CuringMonitor.Api -- import-legacy \
  /path/to/config.txt src/CuringMonitor.Api/assets.json "Curing Press Status"
```
