# OPC interface — what the BodyPly web service tells us

Notes from `bodyplywebservice`, which is the pattern already running in this plant. The
curing service should interface the same way; the differences below are deliberate.

## What BodyPly does

```
config.ini ──► [OPC Server] opcServer = Kepware.KEPServerEX.V6
           └─► [Path] TagConfigPath = …\BodyPlyConfig.csv
                                │
BodyPlyConfig.csv ──────────────┘   wcName, ItemName, ItemAddress, ItemName, ItemAddress, …
                                    BodyPly, hooter, BodyPly1.Device1.Hooter, recipe, BodyPly1.Device1.Recipe, …
                                │
OPCManagerRepository ───────────┘
  LoadConfiguration()        read the CSV into parallel collections (opcItemID / opcItem)
  SmartOPC.Initialize(…)     add every item to one OPC group
  StartData() / StopData()   start and stop the DataChange subscription
  opcRunningState()          health flag
  opcValue[opcItemID.IndexOf("recipe")]   read the cached value by logical name
  WriteData(1, "Ok")         write back to the PLC
```

`SmartLogic.SmartOPC` wraps `OPCAutomation` — **classic OPC DA over COM**. The database
behind it is **PostgreSQL** (`smart_mes`), not SQL Server.

## What to keep

The shape is sound and the team already knows it:

* **Logical name → tag address in a config file.** Application code asks for `recipe`, not
  `BodyPly1.Device1.Recipe`. The curing service does the same thing, reading its map from
  the SCADA press configuration instead of a CSV.
* **One subscription group, cache updated by DataChange.** Poll the cache, not the server.
* **A single connection object with an explicit running state**, so health is observable.
* **Server identity in a config file**, not compiled in.

`IPressDataProvider` / `IOpcSession` in the curing service is that same contract:
`ConnectAsync` ↔ `Initialize`, `SubscribeAsync` ↔ `StartData`, `ReadAsync` ↔ reading
`opcValue`, `IsConnected` ↔ `opcRunningState`.

## What to change

**Carry quality, not just value.** `opcValue` holds a bare value, so a stale reading is
indistinguishable from a fresh one and a dropped PLC keeps its last colour on screen. The
curing service passes value, quality and timestamp together, which is what makes the grey
"no communication" state trustworthy.

**Look up by key, not by index.** `opcItemID.IndexOf("recipe")` is an O(n) scan into
parallel collections held in a `AutoCompleteStringCollection` — a Windows Forms type, in a
web service. At 524 curing tags that pattern costs real time on every poll. A dictionary
keyed by tag address replaces it.

**Prefer OPC UA.** BodyPly uses classic DA because that is what it inherited. The plant
runs **KEPServerEX V6, which exposes an OPC UA endpoint**, and UA gives us: no DCOM, no
Windows-only dependency, proper certificates instead of null sessions, and quality codes
carried natively. Recommended path for curing:

| Option | When | Cost |
|---|---|---|
| **OPC UA client** (`OPCFoundation.NetStandard.Opc.Ua.Client`) | UA endpoint enabled on KEPServerEX V6 | certificate exchange with the server, one adapter class |
| **Classic DA** via `OPCAutomation` interop | UA is not licensed or not enabled | Windows-only edge agent, DCOM configuration, and it must run near the server |

Either way it is one class behind `IOpcSession`; the decision does not reach the rest of
the service.

**Read-only.** BodyPly writes to the PLC (`WriteData` for OK/NOK codes) because it is part
of an interlock. The curing display observes only, and `IOpcSession` has no write method —
if plant control is ever wanted it should be a separate service with its own approvals.

## Open question for the site

Is the UA endpoint on KEPServerEX V6 enabled and licensed? That single answer decides
whether the curing service can run on Linux in a container or needs a Windows edge agent
next to the OPC server.
