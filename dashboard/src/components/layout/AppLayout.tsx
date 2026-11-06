import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar } from './Sidebar';

export function AppLayout() {
  // Owned here so the main content offset follows the sidebar width.
  const [collapsed, setCollapsed] = useState(false);

  return (
    <div className="flex h-screen">
      <Sidebar collapsed={collapsed} onToggle={() => setCollapsed((c) => !c)} />
      <main
        className={`flex-1 overflow-y-auto p-6 bg-zinc-950 flex flex-col transition-all duration-200 ${
          collapsed ? 'ml-16' : 'ml-64'
        }`}
      >
        <div className="flex-1 flex flex-col min-h-0">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
