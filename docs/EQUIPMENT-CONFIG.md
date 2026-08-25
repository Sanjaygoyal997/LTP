# Equipment configuration format

Two layouts are accepted, told apart by the header line. Existing files keep working; the
named layout is the one to move to.

## Named layout (recommended)

```
GroupNo#Name#Title#WorkCentre#Threshold#Signal.runCheck#Signal.alarm#Signal.recipe
6#4919#4919#193#2.5#DH3.4919.parameters.internal_pressure#DH3.4919.parameters.Press_Fault#4901.4901.RecipeCode
```

**Column meaning comes from the header, not its position.** Reorder columns, delete ones you
do not use, add new ones — nothing in the service depends on where they sit. `#` or `,` both
work; the delimiter is taken from the header.

### Fixed columns

| Header | Aliases | Meaning |
|---|---|---|
| `GroupNo` | `RowNo`, `Trench` | which group the box is drawn in, and group order |
| `Name` | `PressName` | identifier, unique within its group |
| `Title` | `PressTitle` | the caption on the box; defaults to `Name` |
| `WorkCentre` | `WcID`, `WorkCentreId` | joins this item to the MES production table |
| `Threshold` | `RunThreshold` | running at or above this value; blank uses `Plant:RunThreshold` |
| `RunSignal` | `StatusSignal` | which signal decides this item's state, when it is not the default |

### Signal columns

Any column named **`Signal.<name>`** binds that signal to the address in it. The names are
yours — the service does not assume what a signal measures. Three *roles* have meaning, and
which signal fills each role is configuration:

| Role | Default signal | Used for |
|---|---|---|
| run check | `Signal.runCheck` | the communication check **and** the run/stop threshold |
| alarm | `Signal.alarm` | flashes the box header |
| recipe | `Signal.recipe` | the small line on the box |

The role is named for what it does, not for what it measures: on a curing press the run
check happens to be internal pressure, but on other equipment it might be temperature,
weight or a state word. Point the roles elsewhere in settings:

```json
"Signals": { "RunCheck": "temperature", "Alarm": "fault", "Recipe": "article" }
```

and override one item with a `RunSignal` column, so a single file can hold equipment judged
on different quantities.

Anything else — `Signal.mouldId`, `Signal.cureTime` — is read, published, and available to a
screen as `signal.mouldId`, with no code change. **That is the point of the format:** a new
status or reading is a new column, not a new release.

### Everything else

Any column the service does not recognise becomes an attribute, reachable as
`asset.attributes.<header>`. So a `Line` or `Mould` column can be shown on a box or used to
filter a screen without the service knowing what it is.

## Status rules

| Result | Rule |
|---|---|
| **No communication** (grey) | the run-check signal reads null, bad quality, or is not a number |
| **Curing run** (green) | run check ≥ threshold |
| **Curing stop** (yellow) | run check < threshold |
| **Alarm** | the alarm signal is `1`, `-1` or `true` — flashes the header, leaves the band alone |

The press-open signal is no longer used for run and stop. The threshold is per item from the
`Threshold` column, falling back to `Plant:RunThreshold`.

## Production counts

Cures come from the MES, not from the PLC — `ProdCountA/B/C` are no longer read.

```json
"Production": {
  "Provider": "sql",
  "ConnectionString": "Server=...;Database=SMARTMESBTP;...",
  "RefreshInterval": "00:00:30",
  "ByWorkCentreQuery": "SELECT wcID, SUM(quantity) FROM dbo.CuringProduction WHERE dtandTime >= @from GROUP BY wcID",
  "ByShiftQuery":      "SELECT shift, SUM(quantity) FROM dbo.CuringProduction WHERE dtandTime >= @from GROUP BY shift"
}
```

* The **number on the box** is that item's `WorkCentre` count for the current shift —
  `@from` is the shift start.
* The **Production A / B / C** panel is per-shift totals for the production day — `@from` is
  that day's shift-A start.
* Queried every 30 s rather than every poll: a cure takes minutes, and this is a database
  rather than a PLC. A failed query keeps the last numbers and flags them, instead of
  showing zeros that would read as "nothing produced".

Both queries are settings, so a different table or a `shift` filter is a configuration
change.

## Legacy layout

The original header — `RowNo#PressName#PressTitle#CommunicationCheck#…` — is still read
positionally, so no file has to change to keep the display running. In that mode there is no
work centre, so production counts read zero until the file gains one.

A converted copy of the plant's current file is in
[`examples/equipment.named-format.txt`](examples/equipment.named-format.txt), with
`WorkCentre` and `Threshold` left blank to fill in.
