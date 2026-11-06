import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AppLayout } from '../components/layout/AppLayout';

function renderLayout() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route element={<AppLayout />}>
          <Route path="/" element={<p>Page content</p>} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe('AppLayout / Sidebar', () => {
  it('shifts the main content to match the collapsed sidebar width', () => {
    renderLayout();
    const main = screen.getByRole('main');
    expect(main).toHaveClass('ml-64');

    fireEvent.click(screen.getByRole('button', { name: 'Collapse sidebar' }));

    expect(main).toHaveClass('ml-16');
    expect(main).not.toHaveClass('ml-64');

    fireEvent.click(screen.getByRole('button', { name: 'Expand sidebar' }));
    expect(main).toHaveClass('ml-64');
  });

  it('keeps accessible names on navigation links when collapsed', () => {
    renderLayout();

    fireEvent.click(screen.getByRole('button', { name: 'Collapse sidebar' }));

    for (const label of ['Overview', 'Inbox', 'Issues', 'Approvals', 'Agents', 'Event Log']) {
      expect(screen.getByRole('link', { name: label })).toBeInTheDocument();
    }
    expect(screen.getByRole('button', { name: 'Expand sidebar' })).toHaveAttribute('aria-expanded', 'false');
  });
});
