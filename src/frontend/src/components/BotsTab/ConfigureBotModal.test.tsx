import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import { ConfigureBotModal } from './ConfigureBotModal';
import type { Bot, BotAgent } from './types';

function makeBot(over: Partial<Bot> = {}): Bot {
  return {
    id: 'bot-1',
    username: 'helperbot',
    displayName: 'Helper Bot',
    agentId: 'agent-1',
    agentName: 'Echo Agent',
    isRunning: false,
    agentConfigJson: null,
    tokens: [],
    ...over,
  };
}

function makeAgentWithParam(): BotAgent {
  return {
    id: 'agent-1',
    name: 'Echo Agent',
    description: null,
    category: null,
    manifest: {
      name: 'Echo Agent',
      description: null,
      category: null,
      parameters: [
        {
          name: 'apiKey',
          type: 'string',
          description: null,
          required: false,
          defaultValue: null,
        },
      ],
    },
  };
}

function defaultProps() {
  return {
    bot: makeBot(),
    agent: null as BotAgent | null,
    paramValues: {} as Record<string, string>,
    isSaving: false,
    configError: null as string | null,
    configSuccess: null as string | null,
    onSetParamValues: vi.fn(),
    onSave: vi.fn((e: Event) => e.preventDefault()),
    onClose: vi.fn(),
  };
}

describe('ConfigureBotModal', () => {
  it('renders without crashing and includes the bot display name in the heading', () => {
    const { getByTestId, container } = render(() => (
      <ConfigureBotModal {...defaultProps()} />
    ));
    expect(getByTestId('configure-bot-modal')).toBeInTheDocument();
    expect(container.textContent).toContain('Configure Helper Bot');
  });

  it('shows the no-parameters fallback when the agent has no manifest parameters', () => {
    const { container } = render(() => <ConfigureBotModal {...defaultProps()} />);
    expect(container.textContent).toContain('This agent has no configurable parameters.');
  });

  it('renders param fields when the agent manifest declares parameters', () => {
    const props = { ...defaultProps(), agent: makeAgentWithParam() };
    const { container } = render(() => <ConfigureBotModal {...props} />);
    expect(container.textContent).toContain('apiKey');
    expect(container.textContent).not.toContain('This agent has no configurable parameters.');
  });

  it('invokes onSave when the form is submitted', () => {
    const props = defaultProps();
    const { container } = render(() => <ConfigureBotModal {...props} />);
    const form = container.querySelector('form')!;
    fireEvent.submit(form);
    expect(props.onSave).toHaveBeenCalledTimes(1);
  });

  it('invokes onClose when the close (x) button is clicked', () => {
    const props = defaultProps();
    const { getByTestId } = render(() => <ConfigureBotModal {...props} />);
    fireEvent.click(getByTestId('configure-bot-modal-close'));
    expect(props.onClose).toHaveBeenCalledTimes(1);
  });

  it('disables the save button and shows Saving... while isSaving', () => {
    const { getByTestId } = render(() => (
      <ConfigureBotModal {...defaultProps()} isSaving={true} />
    ));
    const btn = getByTestId('configure-bot-save') as HTMLButtonElement;
    expect(btn).toBeDisabled();
    expect(btn).toHaveTextContent('Saving...');
  });

  it('renders configError alert and configSuccess status when present', () => {
    const props = {
      ...defaultProps(),
      configError: 'Invalid value for apiKey',
      configSuccess: 'Saved!',
    };
    const { getByRole } = render(() => <ConfigureBotModal {...props} />);
    expect(getByRole('alert')).toHaveTextContent('Invalid value for apiKey');
    expect(getByRole('status')).toHaveTextContent('Saved!');
  });
});
