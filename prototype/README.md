# Curing Press Status — Wall Display

Large-screen dashboard showing live status of every curing press, modelled on the
existing SCADA overview screen (bay-wise tile grid, colour-coded status, production
and press totals, legend).

## Running it

Any static web server works — no build step, no dependencies:

```bash
cd curing-dashboard
python3 -m http.server 8080
# open http://localhost:8080/
```

Press **F** (or the ⛶ button) for full screen on the shop-floor display.

## Modes

The screen is driven by `js/data-source.js`:

| URL | Behaviour |
|-----|-----------|
| `/` or `/?mode=auto` | polls the live endpoint, falls back to the simulator if it is unreachable |
| `/?mode=live` | live endpoint only; shows **Gateway offline** if it fails |
| `/?mode=demo` | built-in simulator, for reviewing the layout without the gateway |

`auto` is the default so a network or gateway outage never leaves a blank wall display.

## Live data contract

`CONFIG.endpoint` (default `api/press-status.json`) must return a snapshot of the
whole plant on every poll (default every 5 s). See `api/press-status.sample.json`:

```json
{
  "timestamp": "2026-08-24T10:15:00+05:30",
  "shift": "A",
  "production": { "A": 7706, "B": 2443, "C": 0 },
  "presses": [
    { "id": "4919", "status": "running", "spec": "140912_ULTIMA",
      "remaining": 10, "cures": 34, "curingLine": "A" }
  ]
}
```

* `status` — one of `running`, `stopped`, `alarm`, `no-comm`.
  A press missing from `presses` is drawn as `no-comm`, so a dropped PLC shows
  grey rather than a stale colour.
* `remaining` — minutes left in the cure; shown on the coloured band while running
  (0 otherwise, matching the current SCADA convention).
* `production` — cures per curing line; the footer adds the **Total** column itself.
* **Total Curing Running / Stop** are counted from the tiles, not sent by the gateway.

## Changing the floor layout

`js/layout.js` holds the whole shop-floor arrangement — bays, rows and the press
number in each position. Cells can also be:

```js
{ id: 'T 6', kind: 'label' }  // non-press marker tile (T 1, TRH, …)
{ kind: 'gap' }               // blank position in the grid
```

Nothing else in the app hard-codes press numbers, so re-arranging a bay is a
one-file change.

## Files

```
index.html            screen structure (floor + footer panels)
css/styles.css        theme, tile states, responsive sizing
js/layout.js          bay/row/press layout + status and legend definitions
js/data-source.js     live polling, fallback, demo simulator
js/app.js             rendering, counters, tooltip, full-screen, poll loop
api/                  sample gateway payload
```
