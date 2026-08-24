/**
 * Screen documents. The service validates only the envelope — an id, a title and widgets
 * that name a type — so these types are the real contract, and adding a widget means
 * adding a component and a config entry, with no backend change.
 */

/** A dotted path into the snapshot, e.g. "signal.recipeCode", "production.total". */
export type FieldPath = string;

export interface TooltipRow {
  label: string;
  field: FieldPath;
  unit?: string;
}

export interface TileSpec {
  header?: FieldPath;
  sub?: FieldPath;
  value?: FieldPath;
  colour?: FieldPath;
  tooltip?: TooltipRow[];
}

export interface TrenchTileSpec {
  /** Literal text rather than a field: the unit never varies. */
  sub?: string;
  value?: FieldPath;
}

export interface WidgetBase {
  type: string;
  /** Where the widget is placed. Defaults to "floor". */
  region?: 'floor' | 'footer';
  title?: string;
}

export interface TileGridWidget extends WidgetBase {
  type: 'tile-grid';
  groupBy?: 'trench';
  tile?: TileSpec;
  trenchTile?: TrenchTileSpec;
}

export interface KpiItem {
  label: string;
  field: FieldPath;
  unit?: string;
}

export interface KpiPanelWidget extends WidgetBase {
  type: 'kpi-panel';
  orientation?: 'rows' | 'columns';
  items: KpiItem[];
}

export interface LegendWidget extends WidgetBase {
  type: 'legend';
  order?: string[];
}

export type Widget = TileGridWidget | KpiPanelWidget | LegendWidget | WidgetBase;

export interface ScreenTheme {
  floor?: string;
  panel?: string;
  chrome?: string;
  accent?: string;
  status?: Record<string, string>;
  tile?: { minWidth?: number; maxWidth?: number };
  alarmPulse?: boolean;
}

export interface ScreenDocument {
  id: string;
  title: string;
  theme?: ScreenTheme;
  widgets: Widget[];
}

export interface ScreenSummary {
  id: string;
  title: string;
}
