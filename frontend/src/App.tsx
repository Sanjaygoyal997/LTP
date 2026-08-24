import { useMemo } from 'react';
import { StatusBar } from './components/StatusBar';
import { TopBar } from './components/TopBar';
import { renderWidget } from './components/widgets';
import { usePlantSnapshot } from './hooks/usePlantSnapshot';
import { useScreen } from './hooks/useScreen';
import { themeToCssVariables } from './lib/theme';
import './styles/app.css';

/** Screen to show, from ?screen=… so one build can drive several walls. */
function screenIdFromUrl(): string {
  return new URLSearchParams(window.location.search).get('screen') ?? 'default';
}

export default function App() {
  const screenId = useMemo(screenIdFromUrl, []);
  const { screen, error: screenError, reload } = useScreen(screenId);
  const { snapshot, feed } = usePlantSnapshot({ onScreensChanged: reload });

  const widgets = screen?.widgets ?? [];
  const floorWidgets = widgets.filter((w) => (w.region ?? 'floor') === 'floor');
  const footerWidgets = widgets.filter((w) => w.region === 'footer');

  const problem = screenError;

  return (
    <div className="app" style={themeToCssVariables(screen?.theme)}>
      <TopBar title={screen?.title ?? 'Curing Press Status'} snapshot={snapshot} feed={feed} />

      {screen ? (
        <main className="stage">
          {floorWidgets.map((widget, index) => renderWidget({ widget, snapshot }, `floor-${index}`))}
        </main>
      ) : (
        <main className="stage stage--empty">{problem ?? 'Loading screen…'}</main>
      )}

      <StatusBar>
        {footerWidgets.map((widget, index) => renderWidget({ widget, snapshot }, `footer-${index}`))}
      </StatusBar>
    </div>
  );
}
