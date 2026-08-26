# Screen document reference

`backend/src/CuringMonitor.Api/screens/*.json`. Saving a file re-renders every open display
— no restart. A malformed file is logged and skipped, so a bad edit cannot blank the wall.

Drop in more files for more screens; a display picks one with `?screen=<id>`, and `default`
serves the first alphabetically.

## Shape

```json
{
  "id": "equipment-status",
  "title": "Equipment Status",
  "theme": { ... },
  "widgets": [ { "type": "...", "region": "floor" | "footer", ... } ]
}
```

`region` places a widget: `floor` fills the main area, `footer` sits in the bottom strip.
An unknown `type` renders as a visible error on the screen rather than disappearing.

## Fields

Anywhere a `field` is accepted, these roots resolve:

| Root | Gives | Examples |
|---|---|---|
| `asset.*` | identity and placement | `asset.label`, `asset.group`, `asset.position`, `asset.kind` |
| `asset.attributes.*` | whatever the configuration carries | `asset.attributes.group`, `asset.attributes.name` |
| `signal.*` | any signal wired up for that box | `signal.recipe`, `signal.count`, `signal.pressure`, `signal.open`, `signal.fault` |
| `status` | `running` / `stopped` / `noCommunication` | `status` |
| `status.label` | its display text | `status.label` |
| `production.*` | per-shift production | `production.a`, `production.b`, `production.c`, `production.total` |
| `totals.*` | counts by state | `totals.running`, `totals.stopped`, `totals.alarm`, `totals.noCommunication`, `totals.total` |
| `shift` | current shift letter | `shift` |

Signal names are the plant's own — whatever the configuration binds to a tag is published
under that name. `signal.count` always resolves to the running shift's counter.

## `tile-grid`

```json
{
  "type": "tile-grid",
  "region": "floor",
  "showGroupLabel": false,
  "showGroupRunningCount": false,

  "source": {
    "where":      { "asset.attributes.group": "6" },
    "groupBy":    "asset.group",
    "orderBy":    "asset.position",
    "groupOrder": ["Trench 6", "Trench 5"],
    "groupDescending": false,
    "wrap": "auto",
    "wrapByGroup": { "Trench 4": 12 }
  },

  "tile": {
    "header": "asset.label",
    "sub":    "signal.recipe",
    "value":  "signal.count",
    "colour": "status",
    "tooltip": [
      { "label": "Equipment", "field": "asset.label" },
      { "label": "Pressure",  "field": "signal.pressure", "unit": "kg/cm²" }
    ]
  },

  "tileByKind": { "gauge": { "value": "signal.value" } }
}
```

| Key | Effect |
|---|---|
| `where` | filter. Equality; a list means "any of". Omit to show everything |
| `groupBy` | what forms a block. Any field — try `signal.recipe` to group by what is being cured |
| `orderBy` | order within a block |
| `groupOrder` | pin block order; unlisted blocks follow naturally |
| `wrap` | `"auto"` follows the panel geometry file; a number forces one width everywhere |
| `wrapByGroup` | override one block |
| `tile` | the three lines on a box, and its colour |
| `tileByKind` | per-`kind` overrides of the same |

## `kpi-panel`

```json
{
  "type": "kpi-panel",
  "region": "footer",
  "title": "Production",
  "orientation": "columns",
  "items": [
    { "label": "A",     "field": "production.a" },
    { "label": "Total", "field": "production.total" },
    { "label": "Alarms","field": "totals.alarm" }
  ]
}
```

`orientation` is `columns` (side by side, as Production is drawn) or `rows` (label and value
on one line, as the running/stop totals are). `unit` on an item appends a suffix.

## `legend`

```json
{ "type": "legend", "region": "footer", "title": "Legends",
  "order": ["noCommunication", "running", "stopped", "alarm"] }
```

## `theme`

```json
{
  "floor":  "#d4d4d4",
  "panel":  "#000000",
  "chrome": "#111111",
  "accent": "#00a2e8",
  "status": { "running": "#00e05a", "stopped": "#ffe400",
              "alarm": "#ff1e1e", "noCommunication": "#9a9a9a" },
  "tile":   { "minWidth": 52, "maxWidth": 112 },
  "alarmPulse": true
}
```

`alarmPulse: false` stops the alarm header flashing and leaves it solid red.

## Things this lets you do

**One bay per wall panel** — a screen per group, each panel opening `?screen=trench-6`:

```json
"source": { "where": { "asset.attributes.group": "6" }, "wrap": 8 }
```

**Show live pressure instead of the cure count:**

```json
"tile": { "value": "signal.pressure", "sub": "status.label" }
```

**Group by recipe rather than by bay**, to see what is being cured where:

```json
"source": { "groupBy": "signal.recipe", "orderBy": "asset.label" },
"showGroupLabel": true
```

**Show each block's own running count** next to its label, e.g. "Trench 6 — 28/32 running":

```json
"showGroupLabel": true,
"showGroupRunningCount": true
```

**Add an alarm or not-communicating count** to the totals panel:

```json
{ "label": "Alarms", "field": "totals.alarm" },
{ "label": "Not Communicating", "field": "totals.noCommunication" }
```

**A night theme** — a second screen file with the same widgets and a darker `floor`.

## Adding a widget type

`tile-grid`, `kpi-panel` and `legend` are what exists today. A new type is a React component
plus one entry in `frontend/src/components/widgets/index.tsx`. The service passes screen
documents through untouched, so no backend change is involved.
