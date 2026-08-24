import { resolveField } from './fields';
import type { AssetQuery } from '../screen';
import type { AssetSnapshot, PlantSnapshot } from '../types';

export interface AssetGroup {
  key: string;
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
  const wrap = query?.wrap ?? DEFAULT_WRAP;

  const grouped = new Map<string, AssetSnapshot[]>();
  for (const asset of assets) {
    const key = String(resolveField(groupBy, { snapshot, asset }) ?? '');
    const bucket = grouped.get(key);
    if (bucket) bucket.push(asset);
    else grouped.set(key, [asset]);
  }

  const keys = [...grouped.keys()];
  if (query?.groupOrder?.length) {
    // An explicit order wins; anything not listed keeps its natural position after it.
    const explicit = query.groupOrder;
    keys.sort((a, b) => rank(explicit, a) - rank(explicit, b));
  } else {
    keys.sort((a, b) => a.localeCompare(b, undefined, { numeric: true }) * (query?.groupDescending ? -1 : 1));
  }

  return keys.map((key) => {
    const members = [...grouped.get(key)!].sort((a, b) => compare(
      resolveField(orderBy, { snapshot, asset: a }),
      resolveField(orderBy, { snapshot, asset: b }),
    ));

    const rows: AssetSnapshot[][] = [];
    for (let i = 0; i < members.length; i += wrap) {
      rows.push(members.slice(i, i + wrap));
    }

    return { key, assets: members, rows };
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

function rank(order: string[], key: string): number {
  const index = order.indexOf(key);
  return index === -1 ? order.length : index;
}

function compare(a: unknown, b: unknown): number {
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  return String(a ?? '').localeCompare(String(b ?? ''), undefined, { numeric: true });
}
