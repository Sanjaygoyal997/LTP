import { LEGEND_ORDER, STATUS_LABELS, type PressStatus } from '../../types';
import type { LegendWidget } from '../../screen';

/** Colour key. The order is configurable so a site can match its own convention. */
export function Legend({ widget }: { widget: LegendWidget }) {
  const order = (widget.order ?? LEGEND_ORDER) as PressStatus[];

  return (
    <fieldset className="panel">
      {widget.title && <legend>{widget.title}</legend>}
      <div className="legend">
        {order.map((status) => (
          <div className="legend__item" key={status}>
            <span className={`legend__swatch legend__swatch--${status}`} />
            {STATUS_LABELS[status] ?? status}
          </div>
        ))}
      </div>
    </fieldset>
  );
}
