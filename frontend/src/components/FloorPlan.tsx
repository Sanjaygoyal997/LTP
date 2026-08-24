import { useMemo } from 'react';
import type { PlantLayout, PlantSnapshot } from '../types';
import { PressTile } from './PressTile';
import { TrenchTile } from './TrenchTile';

interface FloorPlanProps {
  layout: PlantLayout;
  snapshot: PlantSnapshot | null;
}

/** The shop floor: one block per trench, tiles in the rows the layout defines. */
export function FloorPlan({ layout, snapshot }: FloorPlanProps) {
  const pressesById = useMemo(
    () => new Map((snapshot?.presses ?? []).map((press) => [press.id, press])),
    [snapshot],
  );

  const trenchesByNumber = useMemo(
    () => new Map((snapshot?.trenches ?? []).map((trench) => [trench.number, trench])),
    [snapshot],
  );

  // Tiles are sized off the widest row so every trench lines up on the same grid.
  const widestRow = useMemo(
    () => Math.max(...layout.trenches.flatMap((trench) => trench.rows.map((row) => row.length)), 1),
    [layout],
  );

  return (
    <main className="floor" style={{ ['--columns' as string]: widestRow }}>
      {layout.trenches.map((trench) => (
        <section className="trench" key={trench.number} aria-label={trench.label}>
          {trench.rows.map((row, rowIndex) => (
            // Rows have no identity of their own beyond their position in the trench.
            <div className="trench__row" key={`${trench.number}-${rowIndex}`} style={{ ['--cells' as string]: row.length }}>
              {row.map((cell, cellIndex) => {
                if (cell.kind === 'gap') {
                  return <div className="tile tile--gap" key={`gap-${cellIndex}`} />;
                }

                if (cell.kind === 'trench') {
                  return (
                    <TrenchTile
                      key={`trench-${cell.id}`}
                      label={cell.label}
                      trench={trenchesByNumber.get(Number(cell.id))}
                    />
                  );
                }

                return (
                  <PressTile key={cell.id} label={cell.label} press={pressesById.get(cell.id)} />
                );
              })}
            </div>
          ))}
        </section>
      ))}
    </main>
  );
}
