import { resolveText } from '../../lib/fields';
import type { KpiPanelWidget } from '../../screen';
import type { PlantSnapshot } from '../../types';

interface KpiPanelProps {
  widget: KpiPanelWidget;
  snapshot: PlantSnapshot | null;
}

/** A panel of counters. Columns for the production figures, rows for the press totals. */
export function KpiPanel({ widget, snapshot }: KpiPanelProps) {
  const orientation = widget.orientation ?? 'columns';
  const context = { snapshot };

  return (
    <fieldset className={`panel panel--${orientation}`}>
      {widget.title && <legend>{widget.title}</legend>}

      <div className={orientation === 'rows' ? 'kpi-rows' : 'kpi-columns'}>
        {widget.items.map((item) => (
          <div className={orientation === 'rows' ? 'total-row' : 'counter'} key={item.label}>
            <div className={orientation === 'rows' ? 'total-row__label' : 'counter__label'}>{item.label}</div>
            <div className="value-box">
              {resolveText(item.field, context, '0')}
              {item.unit ? <span className="value-box__unit"> {item.unit}</span> : null}
            </div>
          </div>
        ))}
      </div>
    </fieldset>
  );
}
