import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LlmModeBadge } from '../components/LlmModeBadge';

describe('LlmModeBadge', () => {
  it('shows "Demo Mode" when responses are simulated', () => {
    render(<LlmModeBadge demoMode llmProvider="Demo" llmModel="demo-simulated" />);

    expect(screen.getByText('Demo Mode')).toBeInTheDocument();
    expect(screen.queryByText(/demo-simulated/)).not.toBeInTheDocument();
  });

  it('shows provider and model when a real LLM is configured', () => {
    render(<LlmModeBadge demoMode={false} llmProvider="OpenAI" llmModel="gpt-4o-mini" />);

    expect(screen.getByText('OpenAI · gpt-4o-mini')).toBeInTheDocument();
    expect(screen.getByLabelText('LLM model: OpenAI · gpt-4o-mini')).toBeInTheDocument();
    expect(screen.queryByText('Demo Mode')).not.toBeInTheDocument();
  });

  it('shows only the model when the provider is unknown', () => {
    render(<LlmModeBadge demoMode={false} llmProvider={null} llmModel="gpt-4o-mini" />);

    expect(screen.getByText('gpt-4o-mini')).toBeInTheDocument();
  });

  it('renders nothing while the model is unknown', () => {
    const { container } = render(<LlmModeBadge demoMode={false} llmProvider={null} llmModel={null} />);

    expect(container).toBeEmptyDOMElement();
  });
});
