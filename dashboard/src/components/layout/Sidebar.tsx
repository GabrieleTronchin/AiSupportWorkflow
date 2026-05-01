import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  Mail,
  ClipboardList,
  Bot,
  ScrollText,
  PanelLeftClose,
  PanelLeftOpen,
} from 'lucide-react';

const navItems = [
  { label: 'Overview', icon: LayoutDashboard, to: '/' },
  { label: 'Emails', icon: Mail, to: '/emails' },
  { label: 'Issues', icon: ClipboardList, to: '/issues' },
  { label: 'Agents', icon: Bot, to: '/agents' },
  { label: 'Event Log', icon: ScrollText, to: '/events' },
];

export function Sidebar() {
  const [collapsed, setCollapsed] = useState(false);

  return (
    <aside
      className={`fixed left-0 top-0 h-screen flex flex-col bg-zinc-900 border-r border-zinc-800 transition-all duration-200 ${
        collapsed ? 'w-16' : 'w-64'
      }`}
    >
      <div className="flex items-center h-14 px-4 border-b border-zinc-800">
        {!collapsed && (
          <span className="text-sm font-semibold text-zinc-100 truncate">
            AI Support Workflow
          </span>
        )}
      </div>

      <nav className="flex-1 py-2 flex flex-col gap-1 px-2">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/'}
            className={({ isActive }) =>
              `flex items-center gap-3 px-3 py-2 rounded-md text-sm transition-colors ${
                isActive
                  ? 'bg-zinc-800 text-zinc-100'
                  : 'text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800/50'
              }`
            }
          >
            <item.icon className="h-5 w-5 shrink-0" />
            {!collapsed && <span className="truncate">{item.label}</span>}
          </NavLink>
        ))}
      </nav>

      <div className="px-2 py-2 border-t border-zinc-800">
        <button
          onClick={() => setCollapsed(!collapsed)}
          className="flex items-center justify-center w-full px-3 py-2 rounded-md text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800/50 transition-colors"
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        >
          {collapsed ? (
            <PanelLeftOpen className="h-5 w-5" />
          ) : (
            <PanelLeftClose className="h-5 w-5" />
          )}
        </button>
      </div>
    </aside>
  );
}
