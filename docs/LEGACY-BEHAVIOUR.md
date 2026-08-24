# Legacy application — recovered behaviour

Reference for porting `curingApplication` faithfully. Everything here is evidence-based;
where a statement comes from decompiled IL rather than source, it says so.

## Evidence base

| Component | What we have |
|---|---|
| `curingApplication.exe` | full C# source (`mainForm`, `Trendclass`, `alarmClass`, `shiftClass`, `databaseHelper`, `backGroundWorkerClass`) |
| `WindowsFormsControlMimic1.dll` | **no source** — behaviour below recovered by disassembling the IL |
| `WindowsFormsControlMimic3.dll` | **no source** — same |
| `pressControl.dll` | **no source** — public surface recovered from metadata |
| `SmartLogic.dll`, `SmartOPCProject.dll` | **no source** — OPC wrapper; usage pattern also visible in `bodyplywebservice` |
| `config_AB.txt` | present (86 presses, trenches 4–6) |
| `config_AB.txt`, `trenchSize.txt`, `trenchConfig.txt` | supplied by the plant — 137 boxes across trenches 6, 5, 4, 2, 1, 7 |
| `trenchLimit.txt`, `configPara.txt` | **referenced by code, not supplied** |

`config.txt` is the name the mimic compiles in, but the plant maintains `config_AB.txt`
and that is the file to read. The two carry the same columns and differ only in delimiter,
so the reader takes the delimiter from the header line rather than assuming one.
| `configDB.mdb`, `AlarmsLog`, `TrendsLog` | **not supplied** |

---

## 1. Composition

`curingApplication.exe` is a WinForms shell hosting two user controls plus its own trend
and alarm screens:

| Screen | Implemented in | Purpose |
|---|---|---|
| Press status wall | `WindowsFormsControlMimic1.UserControl1` | the tile grid, production and press totals, header pressures, legend |
| Press parameter detail | `WindowsFormsControlMimic3.UserControl3` | 20 labels of live parameters for one selected press |
| Trend | `mainForm` + `Trendclass` + `AxiPlotX` OCX | cure curves from `TrendsLog` |
| Alarm | `mainForm` + `alarmClass` + `DataGridView` | alarm list from `AlarmsLog`, text from `configDB.mdb` |
| Wait overlay | `waitForm` | shown while history loads |

The shell shows one at a time by setting `TableLayoutPanel` column widths to 0 or 100 —
a tab control by another name.

---

## 2. Press status screen — exact behaviour

*Recovered from `WindowsFormsControlMimic1.backgroundWorkerClass` IL.*

### Tag block per press

`config.txt` (same shape as `config_AB.txt`) columns 3–9 are added to one OPC group **in
file order**, so each press occupies **seven consecutive slots** from its `startIndex`:

| Offset | Column | Meaning |
|---|---|---|
| +0 | CommunicationCheck | `…parameters.internal_pressure` |
| +1 | PressOpen_Close | `…parameters.Press_Open` |
| +2 | Alarm | `…parameters.Press_Fault` |
| +3 | RecipeCode | recipe |
| +4 | ProdCountA | shift A counter |
| +5 | ProdCountB | shift B counter |
| +6 | ProdCountC | shift C counter |

Confirmed independently by the totals worker, which strides the value array by 7.

### Band colour — `backgroundWorker1_DoWork`, 1000 ms per press

```
if   opcValue[+0] is null                                  -> Gray        (no communication)
elif opcValue[+1] in { "1", "-1", "true" (case-insensitive)} -> Yellow      (curing stop)
elif opcValue[+1] in { "0", "false" }                       -> SpringGreen (curing run)
else                                                        -> Gray
```

**The pressure value is never compared against a threshold.** Slot +0 is used only as a
null check — presence of a value means the press is communicating. Run versus stop is
decided entirely by `Press_Open`.

Verified three ways:

1. `readConfiguration` adds `columns[3]`, `[4]`, `[5]`… to the tag list in order, so the
   per-press block is columns 3–9 at offsets +0…+6.
2. That block layout is corroborated inside the worker itself: it reads +3 for the recipe
   and +4/+5/+6 for the shift counters, which are exactly columns 6 and 7/8/9.
3. The only numeric comparisons anywhere in the worker are the `DateTime.Hour` tests for
   the shift. There is no `ldc.r8`, no `Parse`, no `ToDouble` — every colour decision is a
   **string equality** against `"1"`, `"-1"`, `"true"`, `"0"`, `"false"`.

A scan of the whole assembly confirms only `backgroundWorker1_DoWork` ever writes the band
colour (four calls: Gray, Yellow, SpringGreen, Gray), and only `setColour_Event` writes
`pressControl.pressColour`.

Two things reconcile this with the expectation that pressure decides run/stop: the column
is *named* `CommunicationCheck` and holds `internal_pressure`, so pressure is what proves
the press is alive; and `Press_Open` is a PLC-side signal that may itself be derived from a
pressure switch in ladder logic. The threshold, if there is one, lives in the PLC — not in
this application.

### Header colour — same worker

```
if opcValue[+2] in { "1", "-1", "true" }:
        MCColour = (MCColour == Red) ? Azure : Red     # flips every cycle -> blinks
else:   MCColour = Azure
```

**An alarm blinks the press-number header between red and azure at 1 Hz; it does not
change the band colour.** Azure is the normal header background.

### Counter shown on the tile — same worker

Selected by the hour, reading the matching slot:

| Hours | Shift | Slot |
|---|---|---|
| `> 6` and `< 15` | A | +4 |
| `> 14` and `< 23` | B | +5 |
| `>= 0` and `< 7` | C | +6 |

Recipe text comes from slot +3 every cycle.

### Production totals — `backgroundWorker3_DoWork`, 100 ms

Walks the whole value array from index 4 in steps of 7, summing shift A, B and C counters
across every press (`Convert.ToInt16`), with a 20 ms pause per press. Total = A + B + C.

### Press totals — `backgroundWorker4_DoWork`, 100 ms

Iterates the tile controls and counts **by colour**, with a 10 ms pause per tile:

* `pressColour == SpringGreen` → Total Curing Running
* `pressColour == Yellow` → Total Curing Stop

Grey and blinking-red tiles are counted in neither.

### Header pressures — out of scope

`backgroundWorker2_DoWork` reads a second OPC group (`opcTrench`) of five header pressures
— MP, LP, HWSP, HWRP, HYDP — into the "Trench Pressure Detail in Kg/Cm²" box, from
`trenchConfig.txt` with limits in `trenchLimit.txt`. **Not being ported:** this screen shows
equipment status, and the header pressures are a utility reading rather than equipment.

### Tile control — `pressControl.dll`

Three stacked labels with auto-resizing fonts: `pressNameLabel` (background `MCColour`),
`recipeNameLabel`, `pCounterLabel` (background `pressColour`). Properties: `pressName`,
`RecipeName`, `pCounter`, `pressColour`, `MCColour`.

### Layout

`UserControl1.readConfiguration` reads `config.txt`, splitting on **`,`** (the exe's
`config_AB.txt` is `#`-separated — same columns, different delimiter, different reader).
Column 1 → control name, column 2 → tile caption. One `FlowLayoutPanel` per distinct RowNo (trench).

`resize_FlowLayout_Child` sizes the boxes from the panel rather than wrapping them at a
fixed count:

```
areaPerBox = panelWidth * panelHeight * 80 / 100 / boxCount
boxHeight  = round(sqrt(areaPerBox / 1.225))
boxWidth   = round(boxHeight * 1.225)
shrink boxWidth until floor(w/boxWidth) * floor(h/boxHeight) >= boxCount
```

Panel dimensions come from `trenchSize.txt`, whose `id` column is the trench `RowNo`; the
two files join on the trench number. Note that only `curingApplication.exe` reads
`trenchSize.txt` — the mimic itself works from the runtime panel size, which the shell has
already set from that file.

---

## 3. Communication state — two different mechanisms

The application decides "is it talking?" differently in two places:

* **Tile grid:** slot +0 is null → grey. Purely a null check on the OPC value.
* **Trend buttons (`backGroundWorkerClass` in the exe):** re-reads the last line of
  `TrendsLog\<press>\<file>.txt` every 5 s; unchanged for 10 consecutive reads → red;
  missing file → red immediately.

A faithful port needs the first. The second belongs to the trend screen.

---

## 4. Shift definition

`shiftClass.getShiftName` (both in the exe and duplicated inside Mimic1):

| Hours | Shift |
|---|---|
| 7–14 | A |
| 15–22 | B |
| 23, and 0–6 | C |

Log file names use `dd MMM yy` plus the shift letter, and during shift C before 07:00 the
**previous** day is used.

---

## 5. Other screens

### Trend (`Trendclass`)

* File: `<project>\TrendsLog\<press>\<ddMMMyy><shift>.txt`, tab-separated.
* Column 0 is an OLE Automation date (double); columns 1..N are channel values.
* Channel names, colours, axes and visibility come from `configDB.mdb` table
  `opcitemToIndex` (`trendname`, ordered by `srno`).
* Live mode tails the file; history mode calls `LoadDataFromFile` on the OCX.
* Round markers and annotations are held in parallel `ArrayList`s.

### Alarm (`alarmClass`)

* File: `<project>\AlarmsLog\<ddMMMyy>.txt`, comma-separated.
* Column 0 = OLE Automation date, column 1 = alarm code, column 7 = press name.
* Code → message from `configDB.mdb` table `alarm` (`alarmcode`, `alarmmsg`).
* Severity comment in the source: 1 critical, 2 event, 3 normal, 4 warning.
* Polls at 1 s; supports date-range and shift filters; exports to Excel via Office interop.

### Parameter detail (`UserControl3`)

* Reads `configPara.txt`, holds a `tagList` and a `tagListType` per tag.
* Type `"D"` is digital: the value is compared to `"1"` / `"True"` and drives a label
  colour. Other types show the value.
* Up to 20 labels (`labelH1`–`labelH20`), for one press at a time (`setPressName`).

---

## 6. Data stores

| Store | Contents |
|---|---|
| `configDB.mdb` | Jet OLEDB, password `smart26062007`. Tables: `projectSetting` (projectTitle), `trend`, `opcitemToIndex`, `alarm` |
| SQL Server via `SmartLogic` | `shiftMaster`, `wcMaster`, `curingOperatorPlanning` — shift, work centre, manning |
| Flat files | `TrendsLog`, `AlarmsLog`, `EventLogs`, `ErrorLogs` |
| OPC | `Kepware.KEPServerEX.V5` in this build (V6 in the newer BodyPly service) |

Note the newer `bodyplywebservice` uses **PostgreSQL** (`smart_mes`), so the plant is not
purely a SQL Server site.

---

## 7. What cannot be settled without more artefacts

1. `configPara.txt` — the parameter-detail panel. Out of scope: dashboard only.
2. `configDB.mdb` — alarm and trend definitions. Out of scope for the dashboard.
3. `trenchConfig.txt` / `trenchLimit.txt` — header pressures. Out of scope: equipment
   status only.

Settled since: the `T_4` / `T_5` / `T_6` / `TRH` boxes are ordinary configured entries;
`config_AB.txt` is the file to read; and `trenchSize.txt` joins to it on the trench
number.

---

## 8. Corrections this forces on the current build

| Current build | Legacy actually does |
|---|---|
| Run vs stop from `pressure >= MinRunningPressure` | Run vs stop from `Press_Open` alone; pressure is only a null/communication check |
| Alarm colours the band red | Alarm **blinks the header** red/azure at 1 Hz; the band keeps its run/stop colour |
| Alarm counted separately from running/stopped | Legacy counts only green and yellow tiles; alarmed presses still count as whatever their band shows |
| Staleness window drives no-communication | Legacy greys purely on a null value |
| Gauge assets invented per trench | No such concept: header pressures are five labels from a second OPC group |
| Booleans read as typed values | Legacy compares **strings**: `"1"`, `"-1"`, `"true"`, `"0"`, `"false"` |
