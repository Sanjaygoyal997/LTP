# Curing press display

React + TypeScript wall display for `CuringMonitor.Api`.

```bash
npm install
npm run dev     # http://localhost:5173
npm run build   # production bundle in dist/
```

`VITE_API_BASE` points at the backend (`.env.development` targets `http://localhost:5080`).
Leave it unset in production to serve the display from the same origin as the API.

The display subscribes to `/hubs/press-status` over SignalR and falls back to polling
`/api/snapshot` every 5 s if the hub cannot be reached, so a proxy that blocks websockets
degrades the refresh rate rather than blanking the screen. Press **F** for full screen.

Layout comes from `/api/layout` — the tile grid is not hard-coded here.
