import { useCallback, useEffect, useState } from 'react';
import { API_BASE } from '../api/client';
import type { ScreenDocument } from '../screen';

interface ScreenState {
  screen: ScreenDocument | null;
  error: string | null;
}

/**
 * Loads the screen document that drives the whole display.
 *
 * `reload` is called when the service reports that a screen changed on disk, so editing
 * the config re-renders every wall panel without anyone touching them.
 */
export function useScreen(screenId: string): ScreenState & { reload: () => void } {
  const [state, setState] = useState<ScreenState>({ screen: null, error: null });
  const [revision, setRevision] = useState(0);

  const reload = useCallback(() => setRevision((r) => r + 1), []);

  useEffect(() => {
    const controller = new AbortController();

    fetch(`${API_BASE}/api/screens/${encodeURIComponent(screenId)}`, {
      signal: controller.signal,
      cache: 'no-store',
    })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error(
            response.status === 404
              ? `No screen named "${screenId}" on the server`
              : `Screen request failed (${response.status})`,
          );
        }
        return (await response.json()) as ScreenDocument;
      })
      .then((screen) => setState({ screen, error: null }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        // Keep the screen we already have: a bad edit should not blank the wall.
        setState((previous) => ({
          screen: previous.screen,
          error: error instanceof Error ? error.message : 'Screen unavailable',
        }));
      });

    return () => controller.abort();
  }, [screenId, revision]);

  return { ...state, reload };
}
