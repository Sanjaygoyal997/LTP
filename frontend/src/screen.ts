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

export interface WidgetBase {
  type: string;
  /** Where the widget is placed. Defaults to "floor". */
  region?: 'floor' | 'footer';
  title?: string;
}

/**
 * Which boxes a widget draws, and how they are arranged. Membership is a query over the
 * live assets rather than a hand-listed grid.
 */
export interface AssetQuery {
  /** Field path to expected value, or to a list of accepted values. */
  where?: Record<string, string | number | Array<string | number>>;
  /** Field to group boxes by. Defaults to "asset.group". */
  groupBy?: FieldPath;
  /** Explicit group order; groups not listed follow in natural order. */
  groupOrder?: string[];
  groupDescending?: boolean;
  /** Field to order boxes within a group by. Defaults to "asset.position". */
  orderBy?: FieldPath;
  /** Boxes per row. Defaults to 16. */
  wrap?: number;
}

export interface TileGridWidget extends WidgetBase {
  type: 'tile-grid';
  source?: AssetQuery;
  showGroupLabel?: boolean;
  tile?: TileSpec;
  /** Per-kind overrides, e.g. a gauge showing its reading instead of a cure count. */
  tileByKind?: Record<string, TileSpec>;
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
