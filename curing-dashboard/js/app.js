/* Curing press wall display: renders the floor, keeps the tiles in sync with
   the latest snapshot, and maintains the footer counters. */
(() => {
  const floorEl     = document.getElementById('floor');
  const tooltipEl   = document.getElementById('tooltip');
  const connEl      = document.getElementById('connState');
  const connTextEl  = document.getElementById('connText');
  const clockEl     = document.getElementById('clock');
  const shiftEl     = document.getElementById('shiftLabel');
  const runningEl   = document.getElementById('totalRunning');
  const stopEl      = document.getElementById('totalStop');
  const productionEl = document.getElementById('production');

  const tiles = new Map();   // press id -> { root, no, spec, val }
  const pressIds = [];
  let latest = new Map();    // press id -> press record

  /* ---------- build the floor once ---------- */
  function normalizeCell(cell) {
    if (typeof cell === 'string') return { id: cell, kind: 'press' };
    return { kind: 'press', ...cell };
  }

  function buildFloor() {
    const frag = document.createDocumentFragment();

    PRESS_LAYOUT.bays.forEach(bay => {
      const bayEl = document.createElement('section');
      bayEl.className = 'bay';
      bayEl.setAttribute('aria-label', bay.name);

      bay.rows.forEach(row => {
        const rowEl = document.createElement('div');
        rowEl.className = 'row';
        // one column per cell in this row; the fixed tile width keeps
        // rows of different lengths aligned on the left edge
        rowEl.style.setProperty('--cols', row.length);

        row.forEach(rawCell => {
          const cell = normalizeCell(rawCell);
          if (cell.kind === 'gap') {
            const gapEl = document.createElement('div');
            gapEl.className = 'cell gap';
            rowEl.appendChild(gapEl);
            return;
          }
          rowEl.appendChild(buildTile(cell));
        });

        bayEl.appendChild(rowEl);
      });

      frag.appendChild(bayEl);
    });

    floorEl.appendChild(frag);
  }

  function buildTile(cell) {
    const root = document.createElement('div');
    root.className = 'tile ' + (cell.kind === 'label' ? 'label no-comm' : 'no-comm');
    root.dataset.id = cell.id;

    const no = document.createElement('div');
    no.className = 'no';
    no.textContent = cell.id;

    const spec = document.createElement('div');
    spec.className = 'spec';
    spec.innerHTML = '&nbsp;';

    const val = document.createElement('div');
    val.className = 'val';
    val.textContent = '0';

    root.append(no, spec, val);

    if (cell.kind === 'press') {
      pressIds.push(cell.id);
      tiles.set(cell.id, { root, no, spec, val });
      root.addEventListener('mouseenter', onTileEnter);
      root.addEventListener('mousemove', onTileMove);
      root.addEventListener('mouseleave', onTileLeave);
    }
    return root;
  }

  /* ---------- apply a snapshot ---------- */
  function render(snapshot) {
    latest = new Map(snapshot.presses.map(p => [String(p.id), p]));

    let running = 0, stopped = 0;

    for (const [id, el] of tiles) {
      const p = latest.get(id);
      const status = p ? p.status : STATUS.NO_COMM;

      el.root.className = 'tile ' + status;
      el.spec.textContent = p && p.spec ? p.spec : '';
      if (!el.spec.textContent) el.spec.innerHTML = '&nbsp;';
      el.val.textContent = p ? valueFor(p) : 0;

      if (status === STATUS.RUNNING) running++;
      else if (status === STATUS.STOPPED) stopped++;
    }

    runningEl.textContent = running;
    stopEl.textContent = stopped;
    renderProduction(snapshot.production || {});
    if (snapshot.shift) shiftEl.textContent = 'Shift ' + snapshot.shift;
    refreshTooltip();
  }

  /* Value shown on the coloured band: minutes remaining while curing,
     otherwise 0 — same convention as the existing SCADA screen. */
  function valueFor(p) {
    if (p.status === STATUS.RUNNING) return p.remaining ?? 0;
    return p.value ?? 0;
  }

  function renderProduction(production) {
    const lines = Object.keys(production).sort();
    const total = lines.reduce((s, k) => s + (production[k] || 0), 0);
    const cells = lines.map(k => ({ k, v: production[k] || 0 }));
    cells.push({ k: 'Total', v: total });

    if (productionEl.childElementCount !== cells.length) {
      productionEl.innerHTML = cells.map(c =>
        `<div class="counter" data-k="${c.k}"><div class="k">${c.k}</div><div class="v">0</div></div>`
      ).join('');
    }
    cells.forEach(c => {
      const node = productionEl.querySelector(`.counter[data-k="${c.k}"] .v`);
      if (node) node.textContent = c.v;
    });
  }

  function renderLegend() {
    document.getElementById('legendItems').innerHTML = LEGEND.map(l =>
      `<div class="legend-item"><span class="swatch ${l.status}"></span>${l.label}</div>`
    ).join('');
  }

  /* ---------- tooltip ---------- */
  let hoverId = null;

  function onTileEnter(e) {
    hoverId = e.currentTarget.dataset.id;
    e.currentTarget.classList.add('hl');
    refreshTooltip();
    positionTooltip(e);
  }
  function onTileMove(e) { positionTooltip(e); }
  function onTileLeave(e) {
    e.currentTarget.classList.remove('hl');
    hoverId = null;
    tooltipEl.hidden = true;
  }

  function refreshTooltip() {
    if (!hoverId) return;
    const p = latest.get(hoverId);
    const label = LEGEND.find(l => l.status === (p ? p.status : STATUS.NO_COMM));
    tooltipEl.innerHTML = [
      `<b>Press ${hoverId}</b>`,
      `Status: ${label ? label.label : '—'}`,
      p && p.spec ? `Spec: ${p.spec}` : null,
      p && p.status === STATUS.RUNNING ? `Remaining: ${p.remaining} min` : null,
      p && p.cures != null ? `Cures today: ${p.cures}` : null,
      p && p.curingLine ? `Line: ${p.curingLine}` : null
    ].filter(Boolean).join('<br>');
    tooltipEl.hidden = false;
  }

  function positionTooltip(e) {
    const pad = 14;
    const r = tooltipEl.getBoundingClientRect();
    let x = e.clientX + pad, y = e.clientY + pad;
    if (x + r.width  > innerWidth)  x = e.clientX - r.width  - pad;
    if (y + r.height > innerHeight) y = e.clientY - r.height - pad;
    tooltipEl.style.left = x + 'px';
    tooltipEl.style.top  = y + 'px';
  }

  /* ---------- chrome ---------- */
  function setConn(source, error) {
    if (error && CONFIG.mode === 'live') {
      connEl.className = 'conn down';
      connTextEl.textContent = 'Gateway offline';
    } else if (source === 'live') {
      connEl.className = 'conn ok';
      connTextEl.textContent = 'Live';
    } else {
      connEl.className = 'conn';
      connTextEl.textContent = error ? 'Demo (gateway offline)' : 'Demo data';
    }
  }

  function startClock() {
    const tick = () => { clockEl.textContent = new Date().toLocaleTimeString('en-GB'); };
    tick();
    setInterval(tick, 1000);
  }

  function wireFullscreen() {
    const toggle = () => {
      if (document.fullscreenElement) document.exitFullscreen();
      else document.documentElement.requestFullscreen().catch(() => {});
    };
    document.getElementById('fsBtn').addEventListener('click', toggle);
    addEventListener('keydown', e => { if (e.key === 'f' || e.key === 'F') toggle(); });
  }

  /* ---------- poll loop ---------- */
  async function poll() {
    const { snapshot, source, error } = await DataSource.fetchSnapshot(pressIds);
    setConn(source, error);
    if (snapshot) render(snapshot);
  }

  buildFloor();
  renderLegend();
  startClock();
  wireFullscreen();
  poll();
  setInterval(poll, CONFIG.pollMs);
})();
