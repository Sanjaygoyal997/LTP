import { STATUS_LABELS, type PlantSnapshot, type PressSnapshot, type PressStatus, type TrenchSnapshot } from '../types';
import type { FieldPath } from '../screen';

/**
 * Context a field path is resolved against. Which parts are present depends on where the
 * field appears: a tile has an asset, a KPI has only the plant.
 */
export interface FieldContext {
  snapshot: PlantSnapshot | null;
  press?: PressSnapshot;
  trench?: TrenchSnapshot;
  label?: string;
}

/**
 * Resolves a dotted path from a screen document against live data.
 *
 * Roots are deliberately named after what an engineer editing the config thinks in —
 * `asset`, `signal`, `status`, `production`, `totals`, `trench` — rather than after the
 * shape of the JSON the API happens to send.
 */
export function resolveField(path: FieldPath | undefined, context: FieldContext): unknown {
  if (!path) return undefined;

  const [root, ...rest] = path.split('.');
  const key = rest.join('.');

  switch (root) {
    case 'asset':
      return context.press ? pick(context.press, key === 'title' ? 'title' : key) : context.label;

    case 'signal':
      return context.press ? pick(context.press, key) : undefined;

    case 'status': {
      const status = (context.press?.status ?? 'noCommunication') as PressStatus;
      return key === 'label' ? STATUS_LABELS[status] : status;
    }

    case 'trench':
      return context.trench ? pick(context.trench, key) : undefined;

    case 'production':
      return context.snapshot ? pick(context.snapshot.production, key) : undefined;

    case 'totals':
      return context.snapshot ? pick(context.snapshot.totals, key) : undefined;

    case 'shift':
      return context.snapshot?.shift;

    default:
      return undefined;
  }
}

/** Resolves a field for display, with a fallback for anything missing or unreadable. */
export function resolveText(path: FieldPath | undefined, context: FieldContext, fallback = ''): string {
  const value = resolveField(path, context);
  if (value === null || value === undefined || value === '') return fallback;
  return String(value);
}

export function resolveNumber(path: FieldPath | undefined, context: FieldContext, fallback = 0): number {
  const value = resolveField(path, context);
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function pick(source: object, key: string): unknown {
  return key.split('.').reduce<unknown>(
    (value, part) => (value && typeof value === 'object' ? (value as Record<string, unknown>)[part] : undefined),
    source,
  );
}
