/** Contracts served by CuringMonitor.Api. Keep in step with backend Contracts/Snapshots.cs. */

export type PressStatus = 'noCommunication' | 'running' | 'stopped' | 'alarm';

/**
 * One box on the screen. Everything the client draws comes from here — the label, the
 * group it belongs to, its order within that group, and every signal the plant wired up.
 */
export interface AssetSnapshot {
  id: string;
  /** "press" is evaluated against the curing rules; "gauge" just shows its value. */
  kind: string;
  label: string;
  group: string;
  position: number;
  status: PressStatus;
  /** Free-form metadata from the plant configuration. */
  attributes: Record<string, string>;
  /** Signal name to current value; names are the plant's own vocabulary. */
  signals: Record<string, unknown>;
  updatedAt: string;
}

export interface ProductionTotals {
  a: number;
  b: number;
  c: number;
  total: number;
}

export interface PressTotals {
  running: number;
  stopped: number;
  alarm: number;
  noCommunication: number;
  total: number;
}

/** A group of boxes — a trench, a bay, a line. */
export interface GroupSnapshot {
  key: string;
  label: string;
  /** Order the plant configuration lists the group in. */
  order: number;
  /** Boxes per row for this group, when the plant configuration specifies a width. */
  wrap: number | null;
}

export interface PlantSnapshot {
  timestamp: string;
  shift: string;
  productionDate: string;
  sourceConnected: boolean;
  production: ProductionTotals;
  totals: PressTotals;
  assets: AssetSnapshot[];
  groups: GroupSnapshot[];
}

export const STATUS_LABELS: Record<string, string> = {
  noCommunication: 'No Communication',
  running: 'Curing Run / Pressure Ok',
  stopped: 'Curing Stop',
  alarm: 'Alarm',
};

/** Legend order used when a screen does not specify one. */
export const LEGEND_ORDER: PressStatus[] = ['noCommunication', 'running', 'stopped', 'alarm'];
