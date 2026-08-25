# Equipment Status Display

Wall display showing live curing-press and equipment status, driven by the plant's own
SCADA configuration files. React front end over an ASP.NET Core API.

```
backend/    CuringMonitor.Api — .NET 8 Web API + SignalR
frontend/   React 19 + TypeScript + Vite
docs/       architecture, legacy behaviour, data mapping, run notes
prototype/  the original static mock-up, kept for reference
```

## Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* Node.js 20 or later
* **Windows**, for the OPC DA client — the simulator provider runs anywhere, but the
  project targets `net8.0-windows`

## Run it

Two terminals. Neither half needs a plant connection — the backend defaults to a simulator
that drives all 137 boxes.

```bash
# 1. backend — http://localhost:5080  (Swagger at /swagger)
cd backend
dotnet run --project src/CuringMonitor.Api

# 2. front end — http://localhost:5173
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>. Press **F** for full screen.

## First build

The code has not been compiled yet — it was written without a .NET SDK available. Expect
to clear a few errors on the first `dotnet build`. The project has
`TreatWarningsAsErrors`, which is right for CI but noisy on a first pass; to see genuine
errors only:

```bash
dotnet build backend/CuringMonitor.sln -p:TreatWarningsAsErrors=false
```

Turn it back on once the build is clean.

## Deploy to a machine with nothing installed

`publish.ps1` builds the display into the service and includes the .NET runtime, so the
target machine needs **neither Node nor .NET** — only Windows.

```powershell
.\publish.ps1 -Output C:\CuringStatus
```

Copy that folder to the target and run `CuringMonitor.Api.exe`. The display is then at
<http://localhost:5080> — one process, one port, API and screen together. Other machines on
the network can open the same address by hostname.

Published as **win-x86**, matching the OPC DA automation wrapper, which is normally
registered 32-bit only.

The target still needs the **OPC Core Components** for `Provider: opc`, and network access
to SQL Server for `Production:Provider: sql`. Neither is a .NET dependency.

To run it as a Windows service:

```powershell
sc.exe create CuringStatus binPath= "C:\CuringStatus\CuringMonitor.Api.exe" start= auto
sc.exe start CuringStatus
```

Give the service account read access to the equipment configuration, and to the MES database
if production is enabled — `LocalSystem` will not have it.

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
