import { useEffect, useState } from 'react';

export interface Clock {
  /** Calendar date, e.g. "26 Aug 2026". Spelt month so 08/09 is never ambiguous. */
  date: string;
  time: string;
}

/** Wall-clock date and time, refreshed every second. */
export function useClock(): Clock {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  return {
    date: now.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }),
    time: now.toLocaleTimeString('en-GB'),
  };
}
