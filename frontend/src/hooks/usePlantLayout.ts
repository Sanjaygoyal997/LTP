import { useEffect, useState } from 'react';
import { fetchLayout } from '../api/client';
import type { PlantLayout } from '../types';

interface LayoutState {
  layout: PlantLayout | null;
  error: string | null;
}

/** Loads the tile grid once. The layout is static for the life of the service. */
export function usePlantLayout(): LayoutState {
  const [state, setState] = useState<LayoutState>({ layout: null, error: null });

  useEffect(() => {
    const controller = new AbortController();

    fetchLayout(controller.signal)
      .then((layout) => setState({ layout, error: null }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        setState({ layout: null, error: error instanceof Error ? error.message : 'Layout unavailable' });
      });

    return () => controller.abort();
  }, []);

  return state;
}
