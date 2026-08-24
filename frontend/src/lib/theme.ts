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

  // Only the bounds come from the theme. The width itself is computed in the stylesheet,
  // on the element that knows the column count — resolving var(--columns) here would
  // capture the default rather than the grid's own value.
  style['--tile-min'] = `${theme.tile?.minWidth ?? 52}px`;
  style['--tile-max'] = `${theme.tile?.maxWidth ?? 112}px`;

  if (theme.alarmPulse === false) style['--alarm-animation'] = 'none';

  return style as React.CSSProperties;
}
