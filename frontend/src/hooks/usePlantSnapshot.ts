import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useEffect, useRef, useState } from 'react';
import { API_BASE, fetchSnapshot } from '../api/client';
import type { PlantSnapshot } from '../types';

export type FeedState = 'connecting' | 'live' | 'polling' | 'offline';

interface SnapshotState {
  snapshot: PlantSnapshot | null;
  feed: FeedState;
}

interface SnapshotOptions {
  /** Called when the service reports that a screen document changed on disk. */
  onScreensChanged?: () => void;
}

/** How often the polling fallback re-reads the snapshot when the hub is unavailable. */
const POLL_INTERVAL_MS = 5000;

/**
 * Subscribes to the live press feed.
 *
 * SignalR is the primary channel; if the hub cannot be reached the hook falls back to
 * polling the snapshot endpoint, so a proxy that blocks websockets degrades the refresh
 * rate instead of leaving the wall display blank.
 */
export function usePlantSnapshot({ onScreensChanged }: SnapshotOptions = {}): SnapshotState {
  const [state, setState] = useState<SnapshotState>({ snapshot: null, feed: 'connecting' });
  const pollTimer = useRef<number | null>(null);

  // Held in a ref so changing the callback does not tear down and rebuild the connection.
  const onScreensChangedRef = useRef(onScreensChanged);
  onScreensChangedRef.current = onScreensChanged;

  useEffect(() => {
    let disposed = false;
    const controller = new AbortController();

    const stopPolling = () => {
      if (pollTimer.current !== null) {
        window.clearInterval(pollTimer.current);
        pollTimer.current = null;
      }
    };

    const poll = async () => {
      try {
        const snapshot = await fetchSnapshot(controller.signal);
        if (!disposed) {
          setState((previous) => ({
            snapshot,
            feed: previous.feed === 'live' ? 'live' : 'polling',
          }));
        }
      } catch {
        if (!disposed) {
          setState((previous) => ({ ...previous, feed: 'offline' }));
        }
      }
    };

    const startPolling = () => {
      if (pollTimer.current !== null) return;
      void poll();
      pollTimer.current = window.setInterval(() => void poll(), POLL_INTERVAL_MS);
    };

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/press-status`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('Snapshot', (snapshot: PlantSnapshot) => {
      if (disposed) return;
      stopPolling();
      setState({ snapshot, feed: 'live' });
    });

    connection.on('ScreensChanged', () => {
      if (!disposed) onScreensChangedRef.current?.();
    });

    connection.onreconnecting(() => {
      if (!disposed) setState((previous) => ({ ...previous, feed: 'connecting' }));
    });

    connection.onclose(() => {
      if (!disposed) {
        setState((previous) => ({ ...previous, feed: 'polling' }));
        startPolling();
      }
    });

    connection
      .start()
      .then(() => {
        if (!disposed) setState((previous) => ({ ...previous, feed: 'live' }));
      })
      .catch(() => {
        if (!disposed) startPolling();
      });

    // Seed immediately so the screen paints before the first push arrives.
    void poll();

    return () => {
      disposed = true;
      controller.abort();
      stopPolling();
      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop();
      }
    };
  }, []);

  return state;
}
