import { Outlet } from 'react-router-dom';
import { Sidebar } from './Sidebar';

export function AppLayout() {
  return (
    <div className="flex h-screen">
      <Sidebar />
      <main className="flex-1 ml-64 overflow-y-auto p-6 bg-zinc-950">
        <Outlet />
      </main>
    </div>
  );
}
