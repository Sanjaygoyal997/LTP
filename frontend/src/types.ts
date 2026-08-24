/** Contracts served by CuringMonitor.Api. Keep in step with backend/src/CuringMonitor.Api/Contracts. */

export type PressStatus = 'noCommunication' | 'running' | 'stopped' | 'alarm';

export type ShiftName = 'A' | 'B' | 'C';

export interface PressSnapshot {
  id: string;
  title: string;
  trench: number;
  status: PressStatus;
  recipeCode: string | null;
  /** Cures booked by this press in the current shift. */
  count: number;
  /** Internal pressure in kg/cm², null when the tag is unreadable. */
  pressure: number | null;
  updatedAt: string;
}

export interface TrenchSnapshot {
  number: number;
  label: string;
  pressure: number | null;
  isHealthy: boolean;
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

export interface PlantSnapshot {
  timestamp: string;
  shift: ShiftName;
  productionDate: string;
  sourceConnected: boolean;
  production: ProductionTotals;
  totals: PressTotals;
  presses: PressSnapshot[];
  trenches: TrenchSnapshot[];
}

export type LayoutCellKind = 'press' | 'trench' | 'gap';

export interface LayoutCell {
  kind: LayoutCellKind;
  id: string;
  label: string;
}

export interface TrenchLayout {
  number: number;
  label: string;
  rows: LayoutCell[][];
}

export interface PlantLayout {
  title: string;
  trenches: TrenchLayout[];
}

export const STATUS_LABELS: Record<PressStatus, string> = {
  noCommunication: 'No Communication',
  running: 'Curing Run / Pressure Ok',
  stopped: 'Curing Stop',
  alarm: 'Alarm',
};

/** Legend order, matching the operator-facing screen this replaces. */
export const LEGEND_ORDER: PressStatus[] = ['noCommunication', 'running', 'stopped', 'alarm'];
