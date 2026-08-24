/*
 * Data source for the curing press screen.
 *
 * Two modes:
 *   live  - polls CONFIG.endpoint for a JSON snapshot (see README for shape)
 *   demo  - drives the screen from a local simulator so the layout can be
 *           reviewed on the big screen without the MES/OPC gateway
 *
 * The screen falls back to demo automatically when the endpoint is not
 * reachable, so a network outage never leaves a blank wall display.
 */
const CONFIG = {
  endpoint: 'api/press-status.json', // MES / OPC-UA gateway snapshot
  pollMs: 5000,
  mode: new URLSearchParams(location.search).get('mode') || 'auto' // auto|live|demo
};

/* ---------- demo simulator ---------- */
const Simulator = (() => {
  const specs = ['140912_ULTIMA', '140912_ULTIMA', '13575_XF', '155/70', '9/70', '140912_ULTIMA'];
  let state = null;

  function seed(pressIds) {
    state = new Map();
    pressIds.forEach((id, i) => {
      const roll = Math.random();
      const status =
        roll > 0.97 ? STATUS.ALARM :
        roll > 0.93 ? STATUS.NO_COMM :
        roll > 0.55 ? STATUS.STOPPED : STATUS.RUNNING;
      state.set(id, {
        id,
        status,
        spec: specs[i % specs.length],
        remaining: status === STATUS.RUNNING ? 2 * (2 + Math.floor(Math.random() * 12)) : 0,
        cures: 20 + Math.floor(Math.random() * 40),
        curingLine: i % 3 === 0 ? 'A' : (i % 3 === 1 ? 'B' : 'C')
      });
    });
  }

  function tick() {
    for (const p of state.values()) {
      if (p.status === STATUS.RUNNING) {
        p.remaining = Math.max(0, p.remaining - 2);
        if (p.remaining === 0) { p.status = STATUS.STOPPED; p.cures += 1; }
      } else if (p.status === STATUS.STOPPED && Math.random() < 0.28) {
        p.status = STATUS.RUNNING;
        p.remaining = 2 * (4 + Math.floor(Math.random() * 12));
      } else if (Math.random() < 0.01) {
        p.status = Math.random() < 0.5 ? STATUS.ALARM : STATUS.NO_COMM;
        p.remaining = 0;
      } else if ((p.status === STATUS.ALARM || p.status === STATUS.NO_COMM) && Math.random() < 0.25) {
        p.status = STATUS.STOPPED;
      }
    }
  }

  return {
    snapshot(pressIds) {
      if (!state) seed(pressIds); else tick();
      const presses = [...state.values()].map(p => ({ ...p }));
      const production = { A: 0, B: 0, C: 0 };
      presses.forEach(p => { production[p.curingLine] += p.cures; });
      return {
        timestamp: new Date().toISOString(),
        shift: 'A',
        production,
        presses
      };
    }
  };
})();

/* ---------- live fetch with graceful fallback ---------- */
const DataSource = {
  lastLiveOk: false,

  async fetchSnapshot(pressIds) {
    if (CONFIG.mode === 'demo') {
      this.lastLiveOk = false;
      return { snapshot: Simulator.snapshot(pressIds), source: 'demo' };
    }
    try {
      const res = await fetch(CONFIG.endpoint, { cache: 'no-store' });
      if (!res.ok) throw new Error('HTTP ' + res.status);
      const snapshot = await res.json();
      this.lastLiveOk = true;
      return { snapshot, source: 'live' };
    } catch (err) {
      this.lastLiveOk = false;
      if (CONFIG.mode === 'live') return { snapshot: null, source: 'live', error: err };
      return { snapshot: Simulator.snapshot(pressIds), source: 'demo', error: err };
    }
  }
};
