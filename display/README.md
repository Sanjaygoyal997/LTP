# Prebuilt display

The React display, already built. It is checked in so the service can be published from a
machine that has no Node tooling: `CuringMonitor.Api.csproj` links this folder as `wwwroot`,
so `dotnet publish` puts it beside the executable and one process serves both the API and
the screen on one port.

It calls the API on its own origin, so nothing in here needs configuring per site.

Regenerate it after changing anything under `frontend/src`:

```bash
cd frontend
npm ci
npm run build
rm -rf ../display && mkdir ../display && cp -r dist/. ../display/
```

Commit the result together with the source change, otherwise a publish serves the old screen.
