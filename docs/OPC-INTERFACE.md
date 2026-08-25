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

## What was built

**Classic OPC DA**, matching BodyPly, decided by the site. `ClassicOpcSession` implements
`IOpcSession` over the same `Interop.OPCAutomation` assembly the existing services use:

```
new OPCServer()                          → Connect(ProgID, node)
OPCGroups.Add("CuringMonitor")           → UpdateRate, IsActive = true
OPCItems.AddItems(...)                   → tag address to server handle, in batches
OPCGroup.SyncRead(OPCCache, ...)         → value + quality + timestamp, on our own cadence
ServerState == OPCRunning                → IsConnected
```

Two deliberate differences from BodyPly:

* **No DataChange callback.** The service already polls on a cadence, so it reads the group
  cache instead of subscribing. That removes a COM apartment problem and makes the read
  path a plain call rather than a shared mutable buffer.
* **Quality travels with the value.** `SyncRead` returns quality and timestamp per item, so
  a stale or bad tag is distinguishable from a fresh one — which is what makes the grey
  "no communication" state trustworthy.

Per-item `AddItems` errors are recorded rather than thrown: a mistyped address in the
equipment configuration leaves that one box grey instead of stopping the whole subscription.
This matters — six addresses in the plant's current file are malformed.

Consequences of choosing DA, all inherent rather than incidental: the service targets
`net8.0-windows`, needs the **OPC Core Components** on the host, and must be able to reach
the server over DCOM.

## Bitness — the first thing that goes wrong

`OPCDAAuto.dll` is an **in-process** COM server, so it must match the bitness of the process
loading it. It is normally registered **32-bit only**, while a .NET build defaults to 64-bit
on x64 Windows. The result is:

```
System.Runtime.InteropServices.COMException (0x80040154):
Retrieving the COM class factory for component with CLSID
{28E68F9A-8D75-11D1-8DC3-3C302A000000} failed ... Class not registered
```

That CLSID is the automation wrapper's `OPCServer` coclass. "Class not registered" here
almost never means the server is missing — it means the wrapper is registered for the other
bitness.

The project therefore sets `<PlatformTarget>x86</PlatformTarget>`. Sites that have the x64
Core Components installed can remove it and run 64-bit.

To check which is registered:

```powershell
# 64-bit registration
reg query "HKCR\CLSID\{28E68F9A-8D75-11D1-8DC3-3C302A000000}\InprocServer32"

# 32-bit registration
reg query "HKCR\Wow6432Node\CLSID\{28E68F9A-8D75-11D1-8DC3-3C302A000000}\InprocServer32"
```

Whichever returns a path to `OPCDAAuto.dll` is the bitness the service must be built for. If
neither does, the Core Components are not installed at all — Kepware ships them as the *OPC
Core Components Redistributable*, and they can also be registered by hand:

```powershell
regsvr32 "C:\Windows\SysWOW64\OPCDAAuto.dll"   # 32-bit
regsvr32 "C:\Windows\System32\OPCDAAuto.dll"   # 64-bit
```

Note that the Kepware **server** being 64-bit is irrelevant: it runs out of process, so only
the wrapper's bitness matters.

Should the site ever enable the UA endpoint on KEPServerEX V6, the swap is one new class
behind the same interface — `OPCFoundation.NetStandard.Opc.Ua.Client` — and nothing else in
the service changes.

**Read-only.** BodyPly writes to the PLC (`WriteData` for OK/NOK codes) because it is part
of an interlock. The curing display observes only, and `IOpcSession` has no write method —
if plant control is ever wanted it should be a separate service with its own approvals.

## Open question for the site

Is the UA endpoint on KEPServerEX V6 enabled and licensed? That single answer decides
whether the curing service can run on Linux in a container or needs a Windows edge agent
next to the OPC server.
