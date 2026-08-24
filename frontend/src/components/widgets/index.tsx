import type { KpiPanelWidget, LegendWidget, TileGridWidget, Widget } from '../../screen';
import type { PlantSnapshot } from '../../types';
import { KpiPanel } from './KpiPanel';
import { Legend } from './Legend';
import { TileGrid } from './TileGrid';

export interface WidgetProps {
  widget: Widget;
  snapshot: PlantSnapshot | null;
}

/**
 * Widget registry. Adding a widget type means adding a component and an entry here — the
 * service passes screen documents through untouched and never needs to know about it.
 */
const REGISTRY: Record<string, (props: WidgetProps) => React.ReactElement | null> = {
  'tile-grid': ({ widget, snapshot }) => <TileGrid widget={widget as TileGridWidget} snapshot={snapshot} />,

  'kpi-panel': ({ widget, snapshot }) => <KpiPanel widget={widget as KpiPanelWidget} snapshot={snapshot} />,

  legend: ({ widget }) => <Legend widget={widget as LegendWidget} />,
};

export function renderWidget(props: WidgetProps, key: string) {
  const render = REGISTRY[props.widget.type];

  if (!render) {
    // Name the offending type rather than rendering nothing: a typo in the config should
    // be obvious on the screen, not a silently missing panel.
    return (
      <div className="widget-error" key={key}>
        Unknown widget type "{props.widget.type}"
      </div>
    );
  }

  return <div className="widget" key={key}>{render(props)}</div>;
}

export const knownWidgetTypes = Object.keys(REGISTRY);
