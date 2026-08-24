# Plant Monitoring Platform — Target Architecture

Supersedes the earlier port-the-legacy-app design. The curing wall display is the first
screen, not the product: the platform is a configurable plant monitoring layer that other
areas (extrusion, calendering, tyre building) join without new code.

---

## 1. Principles

**Model the plant, not the tags.** The system holds an asset model — Plant → Unit → Area →
Equipment — and each asset has *signals* with meaning (`pressure`, `open`, `fault`). Where
a signal physically comes from is a binding, held in configuration. Nothing above the
ingest layer ever sees a string like `DH.4919.parameters.internal_pressure`.

**Anything that changes on a Tuesday is configuration.** Layouts, tag bindings, state
rules, thresholds, colours, shift boundaries — stored, versioned, hot-reloaded, editable in
the browser. Code changes only when a genuinely new *capability* is needed.

**The display must never lie.** Quality and age travel with every value. A frozen screen,
a dead PLC and a dead server look different from each other and none of them looks like
"everything is fine".

**Acquisition, evaluation, presentation and history are separate concerns.** The history
database can be down for a day without the wall display noticing, and vice versa.

**Edge close to the plant, core in the centre.** Protocol adapters live next to the
equipment; the core service never speaks DCOM.

**Read-only by design.** This platform observes. If plant control is ever wanted, it is a
separate service with its own approvals — never a flag on this one.

---

## 2. Shape

```
 Unit-2 network              Unit-4 network
 ┌───────────────┐           ┌───────────────┐
 │ Edge Agent    │           │ Edge Agent    │     store-and-forward buffer
 │  OPC UA / DA  │           │  OPC UA       │     survives WAN blips
 │  SQL / file   │           │  SQL / file   │
 └───────┬───────┘           └───────┬───────┘
         │  gRPC stream of SignalSample (value, quality, timestamp)
         └───────────────┬───────────┘
                         ▼
              ┌──────────────────────┐
              │  Core Service        │
              │                      │
              │  Ingest ─► Signal    │   hot state: last value + quality + age
              │            Store     │
              │              │       │
              │        State Engine  │   declarative rules per asset type
              │              │       │
              │      ┌───────┴─────┐ │
              │      ▼             ▼ │
              │  Broadcaster   History│  independent consumers
              │  (SignalR)     Writer │
              └──────┬───────────┬────┘
                     │           ▼
                     │      Time-series store
                     ▼
        ┌────────────────────────────┐
        │ React SPA                  │
        │  Runtime viewer  │ Designer│
        └────────────────────────────┘
```

---

## 3. Edge Agent

A small .NET worker deployed once per plant network segment. It owns every protocol
adapter and is the only component that needs Windows (for classic OPC DA).

| Connector | Use |
|---|---|
| **OPC UA** | Primary path. KEPServerEX UA endpoint, or PLCs directly. Subscriptions, not polling. |
| **OPC DA** | Legacy servers with no UA endpoint. Windows-only, hence the edge split. |
| **SQL** | Master data and anything already landing in a database — recipes, work orders, MES counters. Named, parameterised queries defined in config; never free SQL in a binding. |
| **Flat file** | Existing SmartSCADA `TrendsLog` / `AlarmsLog`, CSV drops, weighbridge exports. Tail-and-parse with a declared column map. |
| **MQTT / Modbus** | Not needed today; the connector contract is the same. |

Every connector emits the same thing:

```csharp
readonly record struct SignalSample(
    string SourceId,      // "kepware-u2"
    string Address,       // "DH.4919.parameters.internal_pressure"
    object? Value,
    Quality Quality,      // Good | Bad | Uncertain
    DateTimeOffset SourceTimestamp);
```

The agent buffers to local disk when the core is unreachable and replays on reconnect, so a
network cut costs history but not data.

**Why an edge agent rather than connectors inside the core:** DCOM does not cross
firewalls, plant networks are usually segmented, and one crashing OPC stack should not take
the whole platform with it. It also makes adding Unit-4 a deployment, not a code change.

---

## 4. Core Service

### Signal Store
In-memory current-value table keyed by canonical signal id (`press/4919:pressure`), holding
value, quality and both source and receive timestamps. Bounded channels between ingest and
evaluation give backpressure rather than unbounded memory growth when a connector floods.

### Asset model
Assets are instances of **asset types**. A type declares the signals it has, the states it
can be in and the rules that decide between them. Adding curing presses is data; adding
*extruders* is one new type document.

### State Engine
Rules are declarative and evaluated on change, not on a timer:

```yaml
states:
  order: [no-comm, alarm, stopped, running]   # first match wins
  rules:
    - state: no-comm  when: stale(pressure, 30s) and stale(open, 30s)
    - state: alarm    when: fault
    - state: stopped  when: open
    - state: running  when: pressure >= params.minRunningPressure
    - state: stopped  when: true              # explicit fallback
```

Expressions compile once into LINQ expression trees from a restricted grammar — comparison,
boolean logic, arithmetic, and a fixed function set (`stale`, `age`, `avg`, `count`,
`shiftSwitch`). No arbitrary code execution, no scripting engine to sandbox.

This is the piece that makes the platform general. The legacy screen hard-coded
"green if pressure ok, yellow if open, red if fault, grey if silent"; here that is five
lines of configuration that an engineer can change without a release.

### Aggregation
Rollups are configured the same way: `count(state == running)` scoped to a query over the
asset tree. Trench totals, unit totals and plant totals are the same mechanism at different
scopes.

### Broadcaster
SignalR, with one group per open screen. Clients receive a full snapshot on connect and
**deltas** afterwards — only assets whose state, counter or recipe actually changed. At
~1,500 signals a second-by-second full snapshot is wasteful; deltas keep a wall display
under a few KB/s.

### History Writer
A separate consumer of the same stream. It records **state transitions and counter
changes**, not every raw sample — that is what makes downtime analysis, OEE and shift
reports cheap later, without storing a value per tag per second forever. Raw trend capture
stays a per-signal opt-in.

Because it is an independent consumer, a database outage degrades reporting and leaves the
live display untouched.

### Config Service
CRUD, validation, versioning, publish and rollback for every configuration document, with
JSON/YAML import-export so configuration can live in git alongside code.

---

## 5. Configuration model

Four document kinds. Full examples in [`docs/examples`](examples).

**Asset type** — what a class of equipment is:

```yaml
id: curing-press
label: Curing Press
signals:
  - { id: pressure, type: number, unit: "kg/cm²" }
  - { id: open,     type: bool }
  - { id: fault,    type: bool }
  - { id: recipe,   type: string }
  - { id: counter,  type: int, scope: shift }
params:
  minRunningPressure: 1.0
states: { ... as above ... }
```

**Asset + bindings** — where one instance's signals come from:

```yaml
asset: press/4919
type: curing-press
path: unit-2/trench-6
position: 1
bindings:
  pressure: opc://kepware-u2/DH.4919.parameters.internal_pressure
  open:     opc://kepware-u2/DH.4919.parameters.Press_Open
  fault:    opc://kepware-u2/DH.4919.parameters.Press_Fault
  recipe:   sql://mes/current-recipe?press=4919
  counter:  shiftSwitch(
              A: opc://kepware-u2/ProductionSimulation.4919.FIRST_SHIFT_COUNTER,
              B: opc://kepware-u2/ProductionSimulation.4919.SECOND_SHIFT_COUNTER,
              C: opc://kepware-u2/ProductionSimulation.4919.THIRD_SHIFT_COUNTER)
```

One URI grammar covers every source the platform talks to:

| Scheme | Example | Resolved by |
|---|---|---|
| `opc://` | `opc://kepware-u2/DH.4919.parameters.Press_Open` | OPC connector |
| `sql://` | `sql://mes/current-recipe?press=4919` | named query in config |
| `file://` | `file://scada-logs/TrendsLog/4919?column=3` | flat-file connector |
| `expr://` | `expr://press-4919/pressure > 0 and not open` | computed in core |

**Screen** — what is drawn:

```yaml
id: curing-wall
title: Curing Press Status
theme: floor-night
widgets:
  - type: tile-grid
    source: { query: "path = unit-2/* and type = curing-press", groupBy: path, orderBy: position }
    tile:   { header: asset.label, sub: signal.recipe, value: signal.counter, colour: state }
  - type: kpi-row
    items:
      - { label: Total Curing Running, expr: "count(state == 'running')" }
      - { label: Total Curing Stop,    expr: "count(state == 'stopped')" }
```

Note `source.query` rather than a hand-listed grid. Commission a press into
`unit-2/trench-6` and it appears on the wall; the layout document does not change. Explicit
placement is still available where the floor arrangement genuinely matters — a query
supplies the members, an optional `placement` block pins positions.

**Theme** — palette, density, typography, so the same screen renders for a 4K wall, a
control-room monitor and a supervisor's phone.

---

## 6. Front end

Two applications over one contract.

**Runtime viewer** — the wall display. Subscribes to a screen, renders widgets, survives
reconnects. Kiosk mode with screen rotation for sites that cycle several views on one
panel.

**Layout Designer** — drag-and-drop editor for screen documents: place widgets, bind them
to asset queries, preview against live data, save as a new version, publish or roll back.
This is what "flexibility to create a layout" should mean in practice — a supervisor
rearranging a bay should not need us.

### UI improvements over the legacy screen

| Improvement | Why it matters on a shop floor |
|---|---|
| **Fit-to-screen density** | One layout renders correctly at 1080p, 4K and on a tablet; no per-screen tuning. |
| **Colour-blind-safe mode** | Red/green status is the worst possible pairing for ~8% of men. Optional hatching and shape cues carry the same information without relying on hue. |
| **Staleness scrim + data-age badge** | A screen frozen by a crashed browser currently looks identical to a healthy plant. Age makes it obvious. |
| **Separate "server unreachable" and "plant unreachable" states** | Operators need to know who to call. |
| **Attention panel** | Ranks longest-stopped and repeat-alarm presses. The grid shows everything; this shows what to act on. |
| **Drill-down drawer** | Click a press: cure curve, last cures, recent alarms, current recipe — without leaving the screen. |
| **Search / jump to press** | 150+ tiles; finding 24807 by eye is slow. |
| **Night and day themes** | Wall panels at 3 a.m. should not floodlight the bay. |
| **Reduced motion / large type** | Accessibility, and flashing tiles are fatiguing over a 12-hour shift. |

---

## 7. Resilience

| Failure | Effect | Recovery |
|---|---|---|
| One PLC silent | Its tiles go grey after `stale` window; everything else unaffected | automatic |
| OPC server restart | Edge agent reconnects with back-off; tiles grey meanwhile | automatic |
| Edge agent ↔ core link down | Agent buffers to disk; core marks that source's assets grey | replay on reconnect |
| Core restart | Displays reconnect, receive fresh snapshot | seconds |
| History DB down | Live display unaffected; history gaps flagged | writer retries, buffers |
| Browser/wall PC frozen | Age badge and scrim make it visible | operator/kiosk watchdog |

---

## 8. Storage

| Data | Store | Notes |
|---|---|---|
| Configuration | Relational, JSON columns, versioned rows | export to git for review |
| Hot state | Memory | rebuilt from the sources in seconds |
| State transitions, counters, downtime | Time-series tables, partitioned by month | the basis for OEE and shift reports |
| Raw trends | Opt-in per signal | do not store 1,500 tags a second by default |

---

## 9. Recommended technology

| Layer | Choice | Why |
|---|---|---|
| Core & edge | .NET 8/9, ASP.NET Core minimal API, `System.Threading.Channels` | matches the team's stack; excellent for long-running ingest |
| OPC UA | `OPCFoundation.NetStandard.Opc.Ua.Client` | maintained, cross-platform |
| Real-time | SignalR (WebSocket, long-poll fallback) | works through corporate proxies |
| Edge ↔ core | gRPC streaming | efficient, bidirectional, well-supported |
| Database | SQL Server, partitioned history | almost certainly already in the plant; PostgreSQL + TimescaleDB if a free stack is acceptable |
| Front end | React + TypeScript, Vite, TanStack Query, dnd-kit for the designer | mainstream, hireable |
| Observability | OpenTelemetry → whatever the plant already runs | per-connector health on the screen itself |
| Auth | OIDC against AD; roles Viewer / Engineer / Admin; kiosk tokens for wall PCs | wall panels log in once and stay up |

---

## 10. Delivery phases

| Phase | Delivers | Risk |
|---|---|---|
| **0 — Mirror** | Flat-file connector reading the existing SmartSCADA logs; new UI live beside the old screen | none: nothing touches the plant |
| **1 — Direct** | OPC UA edge agent for Unit-2 curing; run in parallel and compare against the old screen | low, reversible |
| **2 — Configurable** | Config service + Layout Designer; screens and bindings move out of files | low |
| **3 — Historical** | History writer, shift reports, downtime pareto, OEE | medium |
| **4 — Plantwide** | New asset types for extrusion, calendering, building; Unit-4 agent | medium |

Phase 0 is the important one: it delivers a working new display without a single change on
the plant network, which makes everything after it an easy conversation.

---

## 11. Decisions to confirm

1. **Database** — SQL Server assumed. Confirm edition and whether a separate history
   database is acceptable.
2. **Deployment** — containers on Linux for the core, Windows service for edge agents, is
   the recommendation. If everything must be Windows Server, the core runs there too.
3. **OPC path** — is a UA endpoint available on the existing KEPServerEX, or must Phase 1
   use classic DA?
4. **Identity** — Active Directory / Entra ID for engineer access, and how wall panels
   should authenticate.
5. **Scope beyond curing** — if extrusion and building are in scope within the year, the
   asset-type model should be reviewed against those areas now rather than later.
