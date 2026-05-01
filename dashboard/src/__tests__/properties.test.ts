import { describe, it, expect } from 'vitest';
import * as fc from 'fast-check';
import { capEvents } from '../components/EventLog';
import { mergeIssues } from '../hooks/useIssues';
import { validateEmail } from '../components/EmailComposer';
import type { WorkflowState, WorkflowStage } from '../types';

// Helper: arbitrary WorkflowStage
const arbStage = fc.constantFrom<WorkflowStage>(
  'Received', 'Classified', 'ClassifiedOutOfScope', 'TeamAssigned',
  'AgentAssigned', 'Resolving', 'Resolved', 'CodeChangeGenerated',
  'Failed', 'ManualReviewRequired'
);

// Helper: arbitrary WorkflowState
const arbWorkflowState = fc.record<WorkflowState>({
  issueId: fc.uuid(),
  stage: arbStage,
  lastUpdated: fc.date().map(d => d.toISOString()),
  detail: fc.option(fc.string({ minLength: 1, maxLength: 100 }), { nil: null }),
});

/**
 * **Validates: Requirements 8.4**
 * Event Log cap invariant: For all sequences of N events (N > 100),
 * the rendered list length equals exactly 100 and contains the most recent events.
 */
describe('Property: Event Log cap invariant', () => {
  it('for any events array with length > 100, capEvents returns exactly 100 items', () => {
    fc.assert(
      fc.property(
        fc.array(arbWorkflowState, { minLength: 101, maxLength: 500 }),
        (events) => {
          const capped = capEvents(events);
          expect(capped).toHaveLength(100);
        }
      )
    );
  });

  it('capEvents returns the first 100 (most recent) events', () => {
    fc.assert(
      fc.property(
        fc.array(arbWorkflowState, { minLength: 101, maxLength: 500 }),
        (events) => {
          const capped = capEvents(events);
          expect(capped).toEqual(events.slice(0, 100));
        }
      )
    );
  });
});

/**
 * **Validates: Requirements 6.3, 6.4**
 * Issues merge idempotence: merging the same SSE update array twice
 * produces identical state to merging once.
 */
describe('Property: Issues merge idempotence', () => {
  it('merging the same update array twice produces identical state to merging once', () => {
    fc.assert(
      fc.property(
        fc.array(arbWorkflowState, { minLength: 0, maxLength: 50 }),
        fc.array(arbWorkflowState, { minLength: 1, maxLength: 50 }),
        (existing, updates) => {
          const mergedOnce = mergeIssues(existing, updates);
          const mergedTwice = mergeIssues(mergedOnce, updates);
          expect(mergedTwice).toEqual(mergedOnce);
        }
      )
    );
  });
});

/**
 * **Validates: Requirements 5.6**
 * Email validation completeness: form allows submission iff both
 * subject.trim() and body.trim() are non-empty.
 */
describe('Property: Email validation completeness', () => {
  it('allows submission iff both subject.trim() and body.trim() are non-empty', () => {
    fc.assert(
      fc.property(
        fc.string(),
        fc.string(),
        (subject, body) => {
          const errors = validateEmail(subject, body);
          const hasErrors = Object.keys(errors).length > 0;
          const shouldAllow = subject.trim().length > 0 && body.trim().length > 0;
          expect(hasErrors).toBe(!shouldAllow);
        }
      )
    );
  });
});
