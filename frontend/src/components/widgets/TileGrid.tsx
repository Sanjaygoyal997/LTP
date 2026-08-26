import { useMemo } from 'react';
import { resolveText, type FieldContext } from '../../lib/fields';
import { selectAssets } from '../../lib/query';
import type { TileGridWidget, TileSpec, TooltipRow } from '../../screen';
import type { AssetSnapshot, PlantSnapshot } from '../../types';

interface TileGridProps {
  widget: TileGridWidget;
  snapshot: PlantSnapshot | null;
}

const DEFAULT_TILE: TileSpec = {
  header: 'asset.label',
  sub: 'signal.recipe',
  value: 'signal.count',
  colour: 'status',
};

/** The floor: one block per group, boxes wrapped into rows, all of it from the query. */
export function TileGrid({ widget, snapshot }: TileGridProps) {
  const groups = useMemo(() => selectAssets(snapshot, widget.source), [snapshot, widget.source]);

  // Boxes size off the widest row so every group lines up on one grid.
  const widestRow = useMemo(
    () => Math.max(...groups.flatMap((group) => group.rows.map((row) => row.length)), 1),
    [groups],
  );

  if (groups.length === 0) {
    return <div className="floor floor--empty">No boxes match this screen&rsquo;s query.</div>;
  }

  return (
    <div className="floor" style={{ ['--columns' as string]: widestRow }}>
      {groups.map((group) => (
        <section className="trench" key={group.key} aria-label={group.label}>
          {widget.showGroupLabel && (
            <h2 className="trench__label">
              {group.label}
              {widget.showGroupRunningCount && (
                <span className="trench__count">
                  {' '}
                  {group.assets.filter((asset) => asset.status === 'running').length}/{group.assets.length} running
                </span>
              )}
            </h2>
          )}

          {group.rows.map((row, rowIndex) => (
            <div
              className="trench__row"
              key={`${group.key}-${rowIndex}`}
              style={{ ['--cells' as string]: row.length }}
            >
              {row.map((asset) => (
                <Tile key={asset.id} asset={asset} widget={widget} snapshot={snapshot} />
              ))}
            </div>
          ))}
        </section>
      ))}
    </div>
  );
}

interface TileProps {
  asset: AssetSnapshot;
  widget: TileGridWidget;
  snapshot: PlantSnapshot | null;
}

function Tile({ asset, widget, snapshot }: TileProps) {
  const spec: TileSpec = { ...DEFAULT_TILE, ...widget.tile, ...widget.tileByKind?.[asset.kind] };
  const context: FieldContext = { snapshot, asset };
  const status = resolveText(spec.colour, context, 'noCommunication');

  return (
    <div
      className={`tile tile--${status} tile--kind-${asset.kind}${asset.alarm ? ' tile--alarm' : ''}`}
      title={tooltip(spec.tooltip, context, asset.label)}
    >
      <div className="tile__no">{resolveText(spec.header, context, asset.label)}</div>
      <div className="tile__recipe">{resolveText(spec.sub, context, ' ')}</div>
      <div className="tile__value">{resolveText(spec.value, context, '0')}</div>
    </div>
  );
}

function tooltip(rows: TooltipRow[] | undefined, context: FieldContext, fallback: string): string {
  if (!rows?.length) return fallback;

  return rows
    .map((row) => {
      const value = resolveText(row.field, context);
      if (!value) return null;
      return `${row.label}: ${value}${row.unit ? ` ${row.unit}` : ''}`;
    })
    .filter(Boolean)
    .join('\n');
}
