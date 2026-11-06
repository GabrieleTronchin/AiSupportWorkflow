import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { IssuesPage } from '../pages/IssuesPage';
import { WORKFLOW_STAGES, type WorkflowState } from '../types';

const issues: WorkflowState[] = [
  { issueId: 'awaiting0000000', stage: 'AwaitingApproval', detail: 'Initialize Items', lastUpdated: new Date().toISOString() },
  { issueId: 'failed000000000', stage: 'Failed', detail: 'HTTP 429', lastUpdated: new Date().toISOString() },
];

vi.mock('../hooks/useIssues', () => ({
  useIssues: () => ({ issues, isLoading: false, error: null }),
}));

describe('IssuesPage', () => {
  it('offers every workflow stage in the filter, including AwaitingApproval', () => {
    render(<IssuesPage />);

    const filter = screen.getByLabelText('Filter by stage');
    const options = within(filter).getAllByRole('option').map((o) => o.getAttribute('value'));

    expect(options).toEqual(['All', ...WORKFLOW_STAGES]);
    expect(options).toContain('AwaitingApproval');
  });

  it('filters issues by AwaitingApproval stage', () => {
    render(<IssuesPage />);

    fireEvent.change(screen.getByLabelText('Filter by stage'), { target: { value: 'AwaitingApproval' } });

    expect(screen.getByText('awaiting')).toBeInTheDocument();
    expect(screen.queryByText('failed00')).not.toBeInTheDocument();
  });
});
