import type { PlantSnapshot } from '../types';

/** Base URL of CuringMonitor.Api; empty string means same-origin (production build). */
export const API_BASE = import.meta.env.VITE_API_BASE ?? '';

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, { signal, cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`${path} responded ${response.status}`);
  }
  return (await response.json()) as T;
}

export const fetchSnapshot = (signal?: AbortSignal) => getJson<PlantSnapshot>('/api/snapshot', signal);
