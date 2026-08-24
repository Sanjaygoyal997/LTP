import { FloorPlan } from './components/FloorPlan';
import { StatusBar } from './components/StatusBar';
import { TopBar } from './components/TopBar';
import { usePlantLayout } from './hooks/usePlantLayout';
import { usePlantSnapshot } from './hooks/usePlantSnapshot';
import './styles/app.css';

export default function App() {
  const { layout, error } = usePlantLayout();
  const { snapshot, feed } = usePlantSnapshot();

  return (
    <div className="app">
      <TopBar title={layout?.title ?? 'Curing Press Status'} snapshot={snapshot} feed={feed} />

      {layout ? (
        <FloorPlan layout={layout} snapshot={snapshot} />
      ) : (
        <main className="floor floor--empty">{error ?? 'Loading floor layout…'}</main>
      )}

      <StatusBar snapshot={snapshot} />
    </div>
  );
}
