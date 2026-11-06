import { Routes, Route, Navigate } from 'react-router-dom';
import { AppLayout } from './components/layout/AppLayout';
import { OverviewPage } from './pages/OverviewPage';
import { InboxPage } from './pages/InboxPage';
import { RunsPage } from './pages/RunsPage';
import { AgentsPage } from './pages/AgentsPage';
import { EventLogPage } from './pages/EventLogPage';
import { ApprovalsPage } from './pages/ApprovalsPage';

function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route path="/" element={<OverviewPage />} />
        <Route path="/inbox" element={<InboxPage />} />
        <Route path="/runs" element={<RunsPage />} />
        {/* Former route of the support-only dashboard */}
        <Route path="/issues" element={<Navigate to="/runs" replace />} />
        <Route path="/approvals" element={<ApprovalsPage />} />
        <Route path="/agents" element={<AgentsPage />} />
        <Route path="/events" element={<EventLogPage />} />
      </Route>
    </Routes>
  );
}

export default App;
