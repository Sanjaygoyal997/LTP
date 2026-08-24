import { STATUS_LABELS, type AssetSnapshot, type PlantSnapshot } from '../types';
import type { FieldPath } from '../screen';

/** What a field path is resolved against. A tile has an asset; a KPI has only the plant. */
export interface FieldContext {
  snapshot: PlantSnapshot | null;
  asset?: AssetSnapshot;
}

/**
 * Resolves a dotted path from a screen document against live data.
 *
 * Roots are named for what an engineer editing the config thinks in — `asset`, `signal`,
 * `status`, `production`, `totals` — rather than for the shape of the JSON the API sends.
 * Anything the plant wired up is reachable: `signal.pressure`, `signal.count`, or
 * `asset.attributes.mould` for metadata that only this site has.
 */
export function resolveField(path: FieldPath | undefined, context: FieldContext): unknown {
  if (!path) return undefined;

  const separator = path.indexOf('.');
  const root = separator === -1 ? path : path.slice(0, separator);
  const key = separator === -1 ? '' : path.slice(separator + 1);

  switch (root) {
    case 'asset':
      return context.asset ? pick(context.asset, key) : undefined;

    case 'signal':
      return context.asset?.signals?.[key];

    case 'status':
      return key === 'label'
        ? (STATUS_LABELS[context.asset?.status ?? 'noCommunication'] ?? context.asset?.status)
        : (context.asset?.status ?? 'noCommunication');

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

function pick(source: object, key: string): unknown {
  if (!key) return undefined;

  return key.split('.').reduce<unknown>(
    (value, part) => (value && typeof value === 'object' ? (value as Record<string, unknown>)[part] : undefined),
    source,
  );
}
