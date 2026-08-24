import type { PressSnapshot } from '../types';

interface PressTileProps {
  label: string;
  press: PressSnapshot | undefined;
}

/**
 * One curing press. A press with no snapshot renders grey rather than blank: on the wall
 * display a missing press must read as "not communicating", not as an empty slot.
 */
export function PressTile({ label, press }: PressTileProps) {
  const status = press?.status ?? 'noCommunication';
  const title = press
    ? [
        `Press ${label}`,
        press.recipeCode ? `Recipe: ${press.recipeCode}` : null,
        `Cures this shift: ${press.count}`,
        press.pressure !== null ? `Pressure: ${press.pressure} kg/cm²` : null,
      ]
        .filter(Boolean)
        .join('\n')
    : `Press ${label} — no data`;

  return (
    <div className={`tile tile--${status}`} title={title}>
      <div className="tile__no">{label}</div>
      <div className="tile__recipe">{press?.recipeCode ?? ' '}</div>
      <div className="tile__value">{press?.count ?? 0}</div>
    </div>
  );
}
