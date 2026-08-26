import { useClock } from '../hooks/useClock';
import { useFullscreen } from '../hooks/useFullscreen';
import type { FeedState } from '../hooks/usePlantSnapshot';
import type { PlantSnapshot } from '../types';

interface TopBarProps {
  title: string;
  snapshot: PlantSnapshot | null;
  feed: FeedState;
}

const FEED_LABEL: Record<FeedState, string> = {
  connecting: 'Connecting…',
  live: 'Live',
  polling: 'Polling',
  offline: 'Server unreachable',
};

export function TopBar({ title, snapshot, feed }: TopBarProps) {
  const clock = useClock();
  const toggleFullscreen = useFullscreen();

  // The plant link matters as much as the server link: a reachable API that has lost the
  // OPC session must not read as healthy.
  const health = feed === 'offline' ? 'down' : snapshot?.sourceConnected ? 'ok' : 'degraded';
  const label = feed === 'live' && snapshot && !snapshot.sourceConnected ? 'Data source offline' : FEED_LABEL[feed];

  return (
    <header className="topbar">
      <div className="topbar__title">
        <span className="topbar__plant">{title}</span>
        {snapshot && <span className="topbar__shift">Shift {snapshot.shift}</span>}
      </div>

      <div className="topbar__right">
        <span className={`feed feed--${health}`}>
          <i className="feed__dot" />
          {label}
        </span>
        <span className="topbar__date">{clock.date}</span>
        <span className="topbar__clock">{clock.time}</span>
        <button type="button" className="topbar__button" onClick={toggleFullscreen} title="Full screen (F)">
          ⛶
        </button>
      </div>
    </header>
  );
}
