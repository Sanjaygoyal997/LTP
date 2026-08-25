# Equipment Status Display

Wall display showing live curing-press and equipment status, driven by the plant's own
SCADA configuration files. React front end over an ASP.NET Core API.

```
backend/    CuringMonitor.Api — .NET 8 Web API + SignalR
frontend/   React 19 + TypeScript + Vite
docs/       architecture, legacy behaviour, data mapping, run notes
prototype/  the original static mock-up, kept for reference
```

There are two ways to run this, and they need different things installed. Pick one.

---

# A. Run it on a plant machine

**Nothing to install but Windows.** No .NET, no Node, no SDK, no `npm`.

Build the package **once on a machine that has the tools**:

```powershell
.\publish.ps1 -Output C:\CuringStatus
```

Copy that folder to the plant machine and run:

```powershell
C:\CuringStatus\CuringMonitor.Api.exe
```

The display is at <http://localhost:5080>. One process, one port — the service serves the
screen itself. Wall panels elsewhere on the network open the same address by hostname and
need only a browser.

Two things are still needed on that machine, but neither is a developer tool: the **OPC Core
Components** if `Provider` is `opc`, and network access to SQL Server if
`Production:Provider` is `sql`.

`config_AB.txt`, `trenchSize.txt` and `screens\*.json` sit in the published folder and are
watched, so edits take effect live. Only `appsettings.json` needs a restart.

For a real installation — service registration, firewall, service account, health check and
rollback — follow [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

---

# B. Work on the code

Only for a development machine. Needs:

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [Node.js](https://nodejs.org/) 20 or later — this is what provides `npm`
* Windows, for the OPC DA client

Two terminals. Neither half needs a plant connection — the defaults drive all 137 boxes from
a simulator.

```powershell
# 1. backend — http://localhost:5080  (Swagger at /swagger)
cd backend
dotnet run --project src/CuringMonitor.Api

# 2. display — http://localhost:5173
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>. Press **F** for full screen.

If a build is noisy, `TreatWarningsAsErrors` is on by design; to see genuine errors only:

```powershell
dotnet build backend\CuringMonitor.sln -p:TreatWarningsAsErrors=false
```

---

## Connect it to the plant

1. Point `Plant:LayoutFile` at the real configuration:

   ```json
   "LayoutFile": "\\\\scada-u2\\SmartScada\\Projects\\PCRCuring\\config_AB.txt"
   ```

   `trenchSize.txt` is read from the same folder if present.

2. Switch the data source to OPC:

   ```json
   "Provider": "opc",
   "Opc": { "ServerName": "Kepware.KEPServerEX.V6", "Node": "" }
   ```

   Classic OPC DA over the Automation interface, as the plant's other services use. Needs
   Windows and the OPC Core Components on the host; set `Node` to reach a server on another
   machine. See [docs/OPC-INTERFACE.md](docs/OPC-INTERFACE.md).

## Change what is shown

* **Which boxes, their names and grouping** — the plant's `config_AB.txt`. Edits are picked
  up without a restart.
* **Layout, colours, tile fields** — `backend/src/CuringMonitor.Api/screens/*.json`. Saving
  a change re-renders every open display.

Both are covered in [docs/RUNNING.md](docs/RUNNING.md), and every screen option is listed
in [docs/SCREEN-REFERENCE.md](docs/SCREEN-REFERENCE.md).
