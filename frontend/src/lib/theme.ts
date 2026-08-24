import type { ScreenTheme } from '../screen';

/** Maps a screen's theme block onto the CSS custom properties the stylesheet reads. */
export function themeToCssVariables(theme: ScreenTheme | undefined): React.CSSProperties {
  if (!theme) return {};

  const style: Record<string, string> = {};

  if (theme.floor) style['--floor-bg'] = theme.floor;
  if (theme.panel) style['--panel-bg'] = theme.panel;
  if (theme.chrome) style['--chrome-bg'] = theme.chrome;
  if (theme.accent) style['--accent'] = theme.accent;

  for (const [status, colour] of Object.entries(theme.status ?? {})) {
    style[`--status-${status}`] = colour;
  }

  const min = theme.tile?.minWidth ?? 52;
  const max = theme.tile?.maxWidth ?? 112;
  style['--tile-width'] = `clamp(${min}px, calc((100vw - 3rem) / var(--columns) - 4px), ${max}px)`;

  if (theme.alarmPulse === false) style['--alarm-animation'] = 'none';

  return style as React.CSSProperties;
}
