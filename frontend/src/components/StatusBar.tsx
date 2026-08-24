import { LEGEND_ORDER, STATUS_LABELS, type PlantSnapshot } from '../types';

interface StatusBarProps {
  snapshot: PlantSnapshot | null;
}

/** Footer: production counters, press totals and the colour legend. */
export function StatusBar({ snapshot }: StatusBarProps) {
  const production = snapshot?.production;
  const totals = snapshot?.totals;

  return (
    <footer className="statusbar">
      <fieldset className="panel">
        <legend>Production</legend>
        <div className="counters">
          <Counter label="A" value={production?.a} />
          <Counter label="B" value={production?.b} />
          <Counter label="C" value={production?.c} />
          <Counter label="Total" value={production?.total} />
        </div>
      </fieldset>

      <fieldset className="panel panel--totals">
        <legend>Press</legend>
        <div className="total-row">
          <span className="total-row__label">Total Curing Running</span>
          <span className="value-box">{totals?.running ?? 0}</span>
        </div>
        <div className="total-row">
          <span className="total-row__label">Total Curing Stop</span>
          <span className="value-box">{totals?.stopped ?? 0}</span>
        </div>
      </fieldset>

      <fieldset className="panel">
        <legend>Legends</legend>
        <div className="legend">
          {LEGEND_ORDER.map((status) => (
            <div className="legend__item" key={status}>
              <span className={`legend__swatch legend__swatch--${status}`} />
              {STATUS_LABELS[status]}
            </div>
          ))}
        </div>
      </fieldset>
    </footer>
  );
}

function Counter({ label, value }: { label: string; value: number | undefined }) {
  return (
    <div className="counter">
      <div className="counter__label">{label}</div>
      <div className="value-box">{value ?? 0}</div>
    </div>
  );
}
