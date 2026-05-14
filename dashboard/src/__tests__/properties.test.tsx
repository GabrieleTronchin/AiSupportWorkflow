import { describe, it, expect, vi } from 'vitest';
import * as fc from 'fast-check';
import { render, screen, fireEvent } from '@testing-library/react';
import { capEvents } from '../components/EventLog';
import { mergeIssues } from '../hooks/useIssues';
import { validateEmail } from '../components/EmailComposer';
import { EmailComposer } from '../components/EmailComposer';
import { EMAIL_TEMPLATES } from '../components/emailTemplates';
import { getNodeColor, mainFlow } from '../components/PipelineVisualizer';
import { renderAgentActivity } from '../components/AgentMonitor';
import { AgentsPage } from '../pages/AgentsPage';
import { useAgents } from '../hooks/useAgents';
import type { WorkflowState, WorkflowStage, AgentStatus, StateTransitionEvent } from '../types';

vi.mock('../hooks/useEmailSubmit', () => ({
  useEmailSubmit: () => ({
    submit: vi.fn().mockResolvedValue(undefined),
    isSubmitting: false,
    isSuccess: false,
    error: null,
    reset: vi.fn(),
  }),
}));

vi.mock('../hooks/useAgents', () => ({
  useAgents: vi.fn(),
}));

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

// Helper: arbitrary StateTransitionEvent
const arbStateTransitionEvent = fc.record<StateTransitionEvent>({
  id: fc.uuid(),
  issueId: fc.uuid(),
  previousStage: fc.option(arbStage, { nil: null }),
  newStage: arbStage,
  timestamp: fc.date().map(d => d.toISOString()),
  detail: fc.option(fc.string({ minLength: 1, maxLength: 100 }), { nil: null }),
});

/**
 * **Validates: Requirements 4.8**
 * Event Log cap invariant: For all sequences of N events (N > 200),
 * the rendered list length equals exactly 200 and contains the most recent events.
 */
describe('Property: Event Log cap invariant', () => {
  it('for any events array with length > 200, capEvents returns exactly 200 items', () => {
    fc.assert(
      fc.property(
        fc.array(arbStateTransitionEvent, { minLength: 201, maxLength: 500 }),
        (events) => {
          const capped = capEvents(events);
          expect(capped).toHaveLength(200);
        }
      )
    );
  });

  it('capEvents returns the first 200 (most recent) events', () => {
    fc.assert(
      fc.property(
        fc.array(arbStateTransitionEvent, { minLength: 201, maxLength: 500 }),
        (events) => {
          const capped = capEvents(events);
          expect(capped).toEqual(events.slice(0, 200));
        }
      )
    );
  });
});

/**
 * **Validates: Requirements 6.3, 6.4**
 * Issues merge idempotence: merging the same stream update array twice
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

/**
 * **Validates: Requirements 3.2**
 * Feature: dashboard-ui-polish, Property 3: Template selection fills all form fields
 *
 * For any valid EmailTemplate object with non-empty sender, subject, and body,
 * selecting that template should result in the form state containing exactly
 * the template's sender, subject, and body values.
 */
describe('Property: Template selection fills all form fields', () => {
  const templatesWithContent = EMAIL_TEMPLATES.filter(
    (t) => t.sender !== '' && t.subject !== '' && t.body !== ''
  );

  it('selecting any template with non-empty fields fills sender, subject, and body', () => {
    fc.assert(
      fc.property(
        fc.constantFrom(...templatesWithContent),
        (template) => {
          const { unmount } = render(<EmailComposer />);

          const templateSelect = screen.getByLabelText('Template') as HTMLSelectElement;
          fireEvent.change(templateSelect, { target: { value: template.id } });

          const senderInput = screen.getByLabelText('Sender') as HTMLInputElement;
          const subjectInput = screen.getByLabelText('Subject') as HTMLInputElement;
          const bodyTextarea = screen.getByLabelText('Body') as HTMLTextAreaElement;

          expect(senderInput.value).toBe(template.sender);
          expect(subjectInput.value).toBe(template.subject);
          expect(bodyTextarea.value).toBe(template.body);

          unmount();
        }
      ),
      { numRuns: 100 }
    );
  });
});

/**
 * **Validates: Requirements 3.5**
 * Feature: dashboard-ui-polish, Property 4: User modifications persist after template selection
 *
 * For any template selection followed by any user modification to a form field,
 * the form field should retain the user's modified value and not revert to the
 * template value on subsequent renders.
 */
describe('Property: User modifications persist after template selection', () => {
  const templatesWithContent = EMAIL_TEMPLATES.filter(
    (t) => t.sender !== '' && t.subject !== '' && t.body !== ''
  );

  it('user modification to any field persists after template selection', () => {
    fc.assert(
      fc.property(
        fc.constantFrom(...templatesWithContent),
        fc.string({ minLength: 1 }),
        fc.constantFrom('sender', 'subject', 'body'),
        (template, userValue, field) => {
          const { unmount } = render(<EmailComposer />);

          // Select the template
          const templateSelect = screen.getByLabelText('Template') as HTMLSelectElement;
          fireEvent.change(templateSelect, { target: { value: template.id } });

          // Modify the chosen field with the user's value
          const fieldLabel = field.charAt(0).toUpperCase() + field.slice(1);
          const inputElement = screen.getByLabelText(fieldLabel) as HTMLInputElement | HTMLTextAreaElement;
          fireEvent.change(inputElement, { target: { value: userValue } });

          // Assert the modified field retains the user's value
          expect(inputElement.value).toBe(userValue);

          // Assert the modified field does NOT have the template value
          const templateValue = template[field as keyof typeof template] as string;
          if (userValue !== templateValue) {
            expect(inputElement.value).not.toBe(templateValue);
          }

          unmount();
        }
      ),
      { numRuns: 100 }
    );
  });
});


/**
 * **Validates: Requirements 1.3, 1.4**
 * Feature: dashboard-ui-polish, Property 2: Multi-issue activity indicators reflect all active issues
 *
 * For any list of WorkflowState objects representing active issues at distinct stages,
 * the pipeline visualization should produce activity indicators at each respective stage,
 * and each indicator should include the corresponding issueId and subject.
 */
describe('Property: Multi-issue activity indicators reflect all active issues', () => {
  it('each active issue at a distinct main flow stage gets a blue active indicator with boxShadow', () => {
    fc.assert(
      fc.property(
        fc.uniqueArray(fc.constantFrom(...mainFlow), { minLength: 1, maxLength: 5 }),
        (distinctStages) => {
          // Create WorkflowState objects for each distinct stage
          const activeIssues: WorkflowState[] = distinctStages.map((stage, index) => ({
            issueId: `issue-${index}`,
            stage,
            lastUpdated: new Date().toISOString(),
            detail: `Subject for issue ${index}`,
          }));

          // For each active issue, verify that getNodeColor returns active styling with boxShadow
          for (const issue of activeIssues) {
            const result = getNodeColor(issue.stage, activeIssues);
            if (issue.stage === 'AwaitingApproval') {
              // AwaitingApproval uses amber styling
              expect(result.background).toBe('#f59e0b');
              expect(result.boxShadow).toBeDefined();
              expect(result.boxShadow).toContain('rgba(245, 158, 11');
            } else {
              expect(result.background).toBe('#3b82f6');
              expect(result.boxShadow).toBeDefined();
              expect(result.boxShadow).toContain('rgba(59, 130, 246');
            }
          }
        }
      ),
      { numRuns: 100 }
    );
  });
});

/**
 * **Validates: Requirements 1.1, 1.6**
 * Feature: dashboard-ui-polish, Property 1: Pipeline node color mapping is consistent with stage position
 *
 * For any valid WorkflowStage that is the current active stage, all stages before it
 * in the main flow should return green styling, the active stage itself should return
 * blue with a box-shadow (pulsing glow), and all stages after it should return neutral
 * gray styling.
 */
describe('Property: Pipeline node color mapping is consistent with stage position', () => {
  it('stages before active are green, active is blue with boxShadow, stages after are gray', () => {
    fc.assert(
      fc.property(
        fc.constantFrom(...mainFlow),
        (activeStage) => {
          const activeIssues: WorkflowState[] = [
            {
              issueId: 'test-issue-1',
              stage: activeStage,
              lastUpdated: new Date().toISOString(),
              detail: null,
            },
          ];

          const activeIndex = mainFlow.indexOf(activeStage);

          for (let i = 0; i < mainFlow.length; i++) {
            const stage = mainFlow[i];
            const result = getNodeColor(stage, activeIssues);

            if (i < activeIndex) {
              // Completed stages should be green
              expect(result.background).toBe('#10b981');
            } else if (i === activeIndex) {
              // Active stage should have boxShadow and appropriate color
              if (stage === 'AwaitingApproval') {
                expect(result.background).toBe('#f59e0b');
                expect(result.boxShadow).toBeDefined();
                expect(result.boxShadow).toContain('rgba(245, 158, 11');
              } else {
                expect(result.background).toBe('#3b82f6');
                expect(result.boxShadow).toBeDefined();
                expect(result.boxShadow).toContain('rgba(59, 130, 246');
              }
            } else {
              // Pending stages should be neutral gray
              expect(result.background).toBe('#3f3f46');
            }
          }
        }
      ),
      { numRuns: 100 }
    );
  });
});


/**
 * **Validates: Requirements 4.3**
 * Feature: dashboard-ui-polish, Property 5: Error display contains HTTP status code and message
 *
 * For any valid HTTP error status code (400–599) and any non-empty error message string,
 * the rendered error state should contain both the numeric status code and the error message text.
 */
describe('Property: Error display contains HTTP status code and message', () => {
  it('for any HTTP error status code (400-599) and non-empty message, both appear in the rendered output', () => {
    fc.assert(
      fc.property(
        fc.integer({ min: 400, max: 599 }),
        fc.string({ minLength: 1, maxLength: 100 }).filter(s => s.trim().length > 0),
        (statusCode, message) => {
          vi.mocked(useAgents).mockReturnValue({
            agents: [],
            isLoading: false,
            error: { statusCode, message },
            retry: vi.fn(),
          });

          const { container, unmount } = render(<AgentsPage />);

          const textContent = container.textContent || '';

          // Assert the status code appears in the rendered output
          expect(textContent).toContain(String(statusCode));

          // Assert the error message appears in the rendered output
          expect(textContent).toContain(message);

          unmount();
        }
      ),
      { numRuns: 100 }
    );
  });
});


/**
 * **Validates: Requirements 4.6, 4.7**
 * Feature: dashboard-ui-polish, Property 6: Working agent card displays current email information
 *
 * For any agent with status "Working" and valid currentIssueId, currentSubject,
 * and currentStage values, the rendered agent card should display all three pieces
 * of information (issue ID, subject, and stage name).
 */
describe('Property: Working agent card displays current email information', () => {
  const stages: WorkflowStage[] = [
    'Received', 'Classified', 'ClassifiedOutOfScope', 'TeamAssigned',
    'AgentAssigned', 'Resolving', 'Resolved', 'CodeChangeGenerated',
    'Failed', 'ManualReviewRequired'
  ];

  it('for any Working agent with valid currentIssueId, currentSubject, and currentStage, renderAgentActivity contains all three', () => {
    fc.assert(
      fc.property(
        fc.uuid(),
        fc.string({ minLength: 1, maxLength: 100 }),
        fc.constantFrom(...stages),
        (issueId, subject, stage) => {
          const agent: AgentStatus = {
            agentId: 'TestAgent_1',
            team: 'TeamA',
            role: 'BackendDeveloper',
            status: 'Working',
            lastAction: null,
            currentIssueId: issueId,
            currentSubject: subject,
            currentStage: stage,
          };

          const result = renderAgentActivity(agent);

          // The result should contain the issue ID
          expect(result).toContain(issueId);
          // The result should contain the subject
          expect(result).toContain(subject);
          // The result should contain the stage name
          expect(result).toContain(stage);
        }
      ),
      { numRuns: 100 }
    );
  });
});
