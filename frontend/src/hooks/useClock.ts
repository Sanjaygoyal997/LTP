import { useEffect, useState } from 'react';

/** Wall-clock time, refreshed every second. */
export function useClock(): string {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  return now.toLocaleTimeString('en-GB');
}
