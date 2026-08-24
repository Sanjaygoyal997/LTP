import type { TrenchSnapshot } from '../types';

interface TrenchTileProps {
  label: string;
  trench: TrenchSnapshot | undefined;
}

/** Trench header pressure, in kg/cm². */
export function TrenchTile({ label, trench }: TrenchTileProps) {
  const healthy = trench?.isHealthy ?? false;

  return (
    <div
      className={`tile tile--marker ${healthy ? '' : 'tile--noCommunication'}`}
      title={`${trench?.label ?? label} header pressure`}
    >
      <div className="tile__no">{label}</div>
      <div className="tile__recipe">kg/cm²</div>
      <div className="tile__value">{trench?.pressure ?? 0}</div>
    </div>
  );
}
