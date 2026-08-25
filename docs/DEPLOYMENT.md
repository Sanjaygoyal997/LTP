# Production deployment

The published package contains the .NET runtime and the prebuilt display, so the target
machine needs only Windows. Everything below is done once per machine.

## 1. Build the package

On a machine that has the .NET SDK and Node:

```powershell
git pull
.\publish.ps1 -Output C:\CuringStatus
```

Published as **win-x86**, matching the OPC DA automation wrapper's usual 32-bit
registration. Copy the folder to the target machine.

## 2. Settings the target needs

Edit `C:\CuringStatus\appsettings.json`:

```json
{
  "Urls": "http://0.0.0.0:5080",
  "Plant": {
    "Provider": "opc",
    "LayoutFile": "config_AB.txt",
    "RunThreshold": 2.5,
    "Opc": { "ServerName": "Kepware.KEPServerEX.V6", "Node": "" },
    "Production": { "Provider": "sql" }
  }
}
```

`0.0.0.0` rather than `localhost`, or only the machine itself can open the display.

Put the connection string in `appsettings.Production.json` beside it — that file is
gitignored, so a credential never reaches the repository:

```json
{ "Plant": { "Production": { "ConnectionString": "Server=...;Database=SMARTMESBTP;..." } } }
```

A published build runs in the Production environment by default, so that file is read
automatically.

## 3. Prerequisites on the target

| For | Needed |
|---|---|
| `Provider: opc` | OPC Core Components, and DCOM reachability if `Opc:Node` names another machine |
| `Production:Provider: sql` | network access to SQL Server, and an account with `SELECT` on `dbo.CuringProduction` and `dbo.wcMaster` |

## 4. Open the port

```powershell
New-NetFirewallRule -DisplayName "Curing Equipment Status" `
  -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow
```

Only needed if wall panels or browsers on other machines will open the display.

## 5. Run it as a service

```powershell
sc.exe create CuringStatus binPath= "C:\CuringStatus\CuringMonitor.Api.exe" start= auto
sc.exe description CuringStatus "Curing equipment status wall display"
sc.exe start CuringStatus
```

Restart it automatically if it ever falls over:

```powershell
sc.exe failure CuringStatus reset= 86400 actions= restart/5000/restart/5000/restart/30000
```

**The service account matters.** `LocalSystem` is the default and will not have rights to
the MES database, nor necessarily to a configuration file on a network share. Use a domain
service account where either applies:

```powershell
sc.exe config CuringStatus obj= "DOMAIN\svc_curing" password= "..."
```

Logs go to the Windows event log when running as a service, since there is no console.

## 6. Check it

```powershell
Invoke-RestMethod http://localhost:5080/health
```

```json
{ "status": "ok", "sourceConnected": true, "lastSnapshot": "..." }
```

Then open <http://localhost:5080> and confirm the boxes are not all grey. In the event log,
start-up should read:

```
Environment Production. Process data provider: opc. Production source: sql.
Polling 818 tags across 137 boxes every 00:00:02.
6 of 818 tags were rejected by the server and will read bad.
```

Six rejected tags is expected — the malformed `T_1` and `T_2` addresses in the equipment
configuration. A much larger number means the channel names do not match the OPC project.

## 7. The wall panel

Any machine with a browser. Point it at `http://<server>:5080` and start it in kiosk mode:

```powershell
start msedge --kiosk "http://curing-server:5080" --edge-kiosk-type=fullscreen
```

Press **F** for full screen if you would rather not use kiosk mode.

## Updating

```powershell
sc.exe stop CuringStatus
# copy the new publish output over C:\CuringStatus, keeping appsettings*.json
sc.exe start CuringStatus
```

The equipment configuration and screen documents are watched, so changes to those need no
restart and no redeployment — edit them in place.

## Rolling back

Keep the previous folder. Stop the service, swap the folders, start it again. Nothing
outside the folder is modified — the service holds no state of its own and writes nothing to
the database.
