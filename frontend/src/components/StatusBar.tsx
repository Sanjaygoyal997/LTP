import type { ReactNode } from 'react';

/** Footer strip. What sits in it comes entirely from the screen document. */
export function StatusBar({ children }: { children: ReactNode }) {
  return <footer className="statusbar">{children}</footer>;
}
