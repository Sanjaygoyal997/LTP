# Third-party assemblies

`Interop.OPCAutomation.dll` — the COM interop assembly for the OPC DA Automation 2.0
type library, taken from the plant's existing `curingApplication` build. It is the same
assembly `bodyplywebservice` references, so the OPC client here talks to KEPServerEX
exactly as the services already running on site do.

It is committed so the build is reproducible without regenerating interop from a
registered type library. Running against a server additionally needs the **OPC Core
Components** installed on the host, which is already the case anywhere the existing
applications run.
