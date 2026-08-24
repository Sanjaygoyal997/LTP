import { useMemo } from 'react';
import { resolveNumber, resolveText, type FieldContext } from '../../lib/fields';
import type { TileGridWidget, TileSpec, TooltipRow, TrenchTileSpec } from '../../screen';
import type { PlantLayout, PlantSnapshot, PressSnapshot, TrenchSnapshot } from '../../types';

interface TileGridProps {
  widget: TileGridWidget;
  layout: PlantLayout;
  snapshot: PlantSnapshot | null;
}

const DEFAULT_TILE: TileSpec = {
  header: 'asset.title',
  sub: 'signal.recipeCode',
  value: 'signal.count',
  colour: 'status',
};

/** The floor: one block per group, tiles laid out as the layout defines. */
export function TileGrid({ widget, layout, snapshot }: TileGridProps) {
  const tile = { ...DEFAULT_TILE, ...widget.tile };
  const trenchTile = widget.trenchTile ?? {};

  const pressesById = useMemo(
    () => new Map((snapshot?.presses ?? []).map((press) => [press.id, press])),
    [snapshot],
  );

  const trenchesByNumber = useMemo(
    () => new Map((snapshot?.trenches ?? []).map((trench) => [trench.number, trench])),
    [snapshot],
  );

  // Tiles size off the widest row so every group lines up on one grid.
  const widestRow = useMemo(
    () => Math.max(...layout.trenches.flatMap((group) => group.rows.map((row) => row.length)), 1),
    [layout],
  );

  return (
    <div className="floor" style={{ ['--columns' as string]: widestRow }}>
      {layout.trenches.map((group) => (
        <section className="trench" key={group.number} aria-label={group.label}>
          {group.rows.map((row, rowIndex) => (
            <div
              className="trench__row"
              key={`${group.number}-${rowIndex}`}
              style={{ ['--cells' as string]: row.length }}
            >
              {row.map((cell, cellIndex) => {
                if (cell.kind === 'gap') {
                  return <div className="tile tile--gap" key={`gap-${cellIndex}`} />;
                }

                if (cell.kind === 'trench') {
                  return (
                    <TrenchTile
                      key={`trench-${cell.id}`}
                      label={cell.label}
                      spec={trenchTile}
                      trench={trenchesByNumber.get(Number(cell.id))}
                      snapshot={snapshot}
                    />
                  );
                }

                return (
                  <PressTile
                    key={cell.id}
                    label={cell.label}
                    spec={tile}
                    press={pressesById.get(cell.id)}
                    snapshot={snapshot}
                  />
                );
              })}
            </div>
          ))}
        </section>
      ))}
    </div>
  );
}

interface PressTileProps {
  label: string;
  spec: TileSpec;
  press: PressSnapshot | undefined;
  snapshot: PlantSnapshot | null;
}

/**
 * A press with no snapshot renders grey rather than blank: on a wall display a missing
 * press must read as "not communicating", never as an empty slot.
 */
function PressTile({ label, spec, press, snapshot }: PressTileProps) {
  const context: FieldContext = { snapshot, press, label };
  const status = resolveText(spec.colour, context, 'noCommunication');

  return (
    <div className={`tile tile--${status}`} title={tooltip(spec.tooltip, context, label)}>
      <div className="tile__no">{resolveText(spec.header, context, label)}</div>
      <div className="tile__recipe">{resolveText(spec.sub, context, ' ')}</div>
      <div className="tile__value">{resolveText(spec.value, context, '0')}</div>
    </div>
  );
}

interface TrenchTileProps {
  label: string;
  spec: TrenchTileSpec;
  trench: TrenchSnapshot | undefined;
  snapshot: PlantSnapshot | null;
}

function TrenchTile({ label, spec, trench, snapshot }: TrenchTileProps) {
  const context: FieldContext = { snapshot, trench, label };
  const healthy = trench?.isHealthy ?? false;

  return (
    <div className={`tile tile--marker ${healthy ? '' : 'tile--noCommunication'}`} title={label}>
      <div className="tile__no">{label}</div>
      <div className="tile__recipe">{spec.sub ?? ' '}</div>
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

export { resolveNumber };
