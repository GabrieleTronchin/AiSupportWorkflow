import { Routes, Route } from 'react-router-dom';
import { AppLayout } from './components/layout/AppLayout';
import { OverviewPage } from './pages/OverviewPage';
import { EmailsPage } from './pages/EmailsPage';
import { IssuesPage } from './pages/IssuesPage';
import { AgentsPage } from './pages/AgentsPage';
import { EventLogPage } from './pages/EventLogPage';

function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route path="/" element={<OverviewPage />} />
        <Route path="/emails" element={<EmailsPage />} />
        <Route path="/issues" element={<IssuesPage />} />
        <Route path="/agents" element={<AgentsPage />} />
        <Route path="/events" element={<EventLogPage />} />
      </Route>
    </Routes>
  );
}

export default App;
