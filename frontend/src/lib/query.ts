import { resolveField } from './fields';
import type { AssetQuery } from '../screen';
import type { AssetSnapshot, PlantSnapshot } from '../types';

export interface AssetGroup {
  key: string;
  label: string;
  assets: AssetSnapshot[];
  rows: AssetSnapshot[][];
}

const DEFAULT_WRAP = 16;

/**
 * Turns the boxes in a snapshot into the grid a tile-grid widget draws.
 *
 * Membership is a query, not a hand-listed grid: commission a press into a group and it
 * appears, decommission it and the box goes, with no edit to the screen document.
 */
export function selectAssets(snapshot: PlantSnapshot | null, query: AssetQuery | undefined): AssetGroup[] {
  const assets = (snapshot?.assets ?? []).filter((asset) => matches(asset, query, snapshot));
  if (assets.length === 0) return [];

  const groupBy = query?.groupBy ?? 'asset.group';
  const orderBy = query?.orderBy ?? 'asset.position';

  const grouped = new Map<string, AssetSnapshot[]>();
  for (const asset of assets) {
    const key = String(resolveField(groupBy, { snapshot, asset }) ?? '');
    const bucket = grouped.get(key);
    if (bucket) bucket.push(asset);
    else grouped.set(key, [asset]);
  }

  // Group order comes from the plant configuration unless the screen overrides it: the
  // sequence the plant lists its bays in is the one operators know, and it is not
  // necessarily alphabetical.
  const declared = new Map((snapshot?.groups ?? []).map((group) => [group.key, group]));
  const keys = [...grouped.keys()];

  if (query?.groupOrder?.length) {
    const explicit = query.groupOrder;
    keys.sort((a, b) => rank(explicit, a) - rank(explicit, b));
  } else if (declared.size > 0) {
    keys.sort((a, b) => (declared.get(a)?.order ?? Number.MAX_SAFE_INTEGER)
      - (declared.get(b)?.order ?? Number.MAX_SAFE_INTEGER));
  } else {
    keys.sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  }

  if (query?.groupDescending) {
    keys.reverse();
  }

  return keys.map((key) => {
    const members = [...grouped.get(key)!].sort((a, b) => compare(
      resolveField(orderBy, { snapshot, asset: a }),
      resolveField(orderBy, { snapshot, asset: b }),
    ));

    const perRow = wrapFor(key, declared.get(key)?.wrap ?? null, query);

    const rows: AssetSnapshot[][] = [];
    for (let i = 0; i < members.length; i += perRow) {
      rows.push(members.slice(i, i + perRow));
    }

    return { key, label: declared.get(key)?.label ?? key, assets: members, rows };
  });
}

/**
 * Filters on equality, with an array meaning "any of". Deliberately not an expression
 * language: this is edited by engineers, and a typo should fail visibly, not silently
 * match everything.
 */
function matches(asset: AssetSnapshot, query: AssetQuery | undefined, snapshot: PlantSnapshot | null): boolean {
  const where = query?.where;
  if (!where) return true;

  return Object.entries(where).every(([path, expected]) => {
    const actual = resolveField(path, { snapshot, asset });
    const wanted = Array.isArray(expected) ? expected : [expected];
    return wanted.some((value) => String(value) === String(actual));
  });
}

/**
 * Boxes per row for one group. A per-group override wins; then the width the plant
 * configuration gives the group when the screen asks for "auto"; then the screen's own
 * number; then the default.
 */
function wrapFor(key: string, declaredWrap: number | null, query: AssetQuery | undefined): number {
  const override = query?.wrapByGroup?.[key];
  if (override && override > 0) return override;

  if (query?.wrap === 'auto') {
    return declaredWrap && declaredWrap > 0 ? declaredWrap : DEFAULT_WRAP;
  }

  return typeof query?.wrap === 'number' && query.wrap > 0 ? query.wrap : DEFAULT_WRAP;
}

function rank(order: string[], key: string): number {
  const index = order.indexOf(key);
  return index === -1 ? order.length : index;
}

function compare(a: unknown, b: unknown): number {
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  return String(a ?? '').localeCompare(String(b ?? ''), undefined, { numeric: true });
}
