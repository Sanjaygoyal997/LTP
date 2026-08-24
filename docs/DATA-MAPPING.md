# Data mapping — legacy SCADA source → dashboard datapoint

Every value on the wall display, traced back to the process tag or file it comes from in
the existing `curingApplication` / SmartSCADA installation. The API contract column names
the field in `PlantSnapshot` (see `backend/src/CuringMonitor.Api/Contracts`).

`<PFX>` is the Kepware channel prefix for the press group — `DH`, `PR` or `ET` in the
sample config.

## Press tile

| Tile element | API field | Legacy source | Notes |
|---|---|---|---|
| Header (press number) | `presses[].id` / `.title` | `config_AB.txt` col 2 `PressName`, col 3 `PressTitle` | Static |
| Small line | `presses[].recipeCode` | OPC tag in col 7 `RecipeCode`, e.g. `4901.4901.RecipeCode` | Shared across a press group |
| Number on the coloured band | `presses[].count` | col 8/9/10, chosen by current shift: `…FIRST_SHIFT_COUNTER` (A), `…SECOND_SHIFT_COUNTER` (B), `…THIRD_SHIFT_COUNTER` (C) | Cures booked this shift |
| Band colour | `presses[].status` | derived — see below | |
| Pressure (tooltip) | `presses[].pressure` | col 4 `<PFX>.<press>.parameters.internal_pressure` | kg/cm² |

### Status derivation

Evaluated in this order by `PressStatusEvaluator`:

| Result | Source tag | Condition |
|---|---|---|
| `noCommunication` | col 4 `internal_pressure` (quality) | no good reading from any of the press's tags within `Plant:StaleAfter` |
| `alarm` | col 6 `Alarm` → `<PFX>.<press>.parameters.Press_Fault` | true |
| `stopped` | col 5 `PressOpen_Close` → `<PFX>.<press>.parameters.Press_Open` | true (press open) |
| `running` | col 4 `internal_pressure` | closed and pressure ≥ `Plant:MinRunningPressure` |
| `stopped` | — | closed but below the pressure threshold |

The legacy app inferred communication from file activity instead: it re-read the last line
of `TrendsLog\<press>\<file>.txt` every 5 s and went red after ten unchanged reads. The
service uses tag quality plus a staleness window, which is equivalent but does not depend
on the log writer running.

## Trench tile (`T 4`, `T 5`, `T 6`, `TRH`)

| Element | API field | Legacy source |
|---|---|---|
| Header | `trenches[].label` | `config_AB.txt` col 1 `RowNo` = trench number |
| Value | `trenches[].pressure` | trench header pressure tag; legacy `trenchConfig.txt` + `trenchLimit.txt`, `opcTrench` in `WindowsFormsControlMimic1` |
| Grey when unreadable | `trenches[].isHealthy` | tag quality |

Related utility pressures on the legacy header — `HWSP` (hot water supply), `HWRP` (hot
water return), `HyDp` (hydraulic), `LP`/`MP` (steam) — are not modelled yet; they follow
the same pattern as the trench pressure tag.

## Footer

| Element | API field | Source |
|---|---|---|
| Production **A** | `production.a` | Σ over all presses of `…FIRST_SHIFT_COUNTER` |
| Production **B** | `production.b` | Σ `…SECOND_SHIFT_COUNTER` |
| Production **C** | `production.c` | Σ `…THIRD_SHIFT_COUNTER` |
| Production **Total** | `production.total` | A + B + C |
| Total Curing Running | `totals.running` | count of presses with status `running` |
| Total Curing Stop | `totals.stopped` | count of presses with status `stopped` |
| Legend | static | fixed labels |

## Chrome

| Element | API field | Source |
|---|---|---|
| Shift badge | `shift` | `ShiftService`: A 07:00–14:59, B 15:00–22:59, C 23:00–06:59 |
| Production day | `productionDate` | shift C before 07:00 books against the previous day |
| Feed indicator | `sourceConnected` + client transport state | OPC session state; the client separately reports live / polling / offline |
| Clock | — | client-side |
| Tile grid | `/api/layout` | `config_AB.txt` col 1 (trench) and press order; panel sizes came from `trenchSize.txt` |

## Not modelled yet

| Data | Legacy source |
|---|---|
| Alarm list per press | `AlarmsLog\DDMmmYY.txt` — CSV, `[0]` OLE-Automation date, `[1]` alarm code, `[7]` press. Code → text from `configDB.mdb` table `alarm` |
| Cure curve / trend | `TrendsLog\<press>\DDMmmYY<shift>.txt` — tab-separated, col 0 OADate, cols 1–12 channels. Channel metadata from `configDB.mdb` table `opcitemToIndex` |
| Project title | `configDB.mdb` → `projectSetting.projectTitle` |
| Operator / work centre | SQL Server via `SmartLogic.dll`: `shiftMaster`, `wcMaster`, `curingOperatorPlanning` |
