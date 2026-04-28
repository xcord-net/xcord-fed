import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import { CreateBotModal } from './CreateBotModal';
import type { BotAgent } from './types';

function makeAgent(over: Partial<BotAgent> = {}): BotAgent {
  return {
    id: 'agent-1',
    name: 'Echo Agent',
    description: null,
    category: null,
    manifest: null,
    ...over,
  };
}

function defaultProps() {
  return {
    agents: [] as BotAgent[],
    username: '',
    displayName: '',
    tokenName: '',
    agentId: '',
    paramValues: {} as Record<string, string>,
    isCreating: false,
    createError: null as string | null,
    newToken: null as string | null,
    copiedToken: false,
    onSetUsername: vi.fn(),
    onSetDisplayName: vi.fn(),
    onSetTokenName: vi.fn(),
    onAgentChange: vi.fn(),
    onSetParamValues: vi.fn(),
    onSubmit: vi.fn((e: Event) => e.preventDefault()),
    onDismiss: vi.fn(),
    onCopyToken: vi.fn(),
  };
}

describe('CreateBotModal', () => {
  it('renders without crashing with minimal valid props', () => {
    const { getByTestId } = render(() => <CreateBotModal {...defaultProps()} />);
    expect(getByTestId('create-bot-modal')).toBeInTheDocument();
  });

  it('renders the form fields when no token has been issued yet', () => {
    const { getByTestId, container } = render(() => <CreateBotModal {...defaultProps()} />);
    expect(getByTestId('create-bot-username')).toBeInTheDocument();
    expect(getByTestId('create-bot-display-name')).toBeInTheDocument();
    expect(getByTestId('create-bot-token-name')).toBeInTheDocument();
    expect(container.textContent).toContain('Create Bot');
  });

  it('emits onSetUsername when typing into the username field', () => {
    const props = defaultProps();
    const { getByTestId } = render(() => <CreateBotModal {...props} />);
    fireEvent.input(getByTestId('create-bot-username'), { target: { value: 'mybot' } });
    expect(props.onSetUsername).toHaveBeenCalledWith('mybot');
  });

  it('invokes onSubmit when the form is submitted', () => {
    const props = {
      ...defaultProps(),
      username: 'mybot',
      displayName: 'My Bot',
      tokenName: 'main',
    };
    const { container } = render(() => <CreateBotModal {...props} />);
    const form = container.querySelector('form')!;
    fireEvent.submit(form);
    expect(props.onSubmit).toHaveBeenCalledTimes(1);
  });

  it('disables the submit button and shows Creating... when isCreating', () => {
    const { getByTestId } = render(() => (
      <CreateBotModal {...defaultProps()} isCreating={true} />
    ));
    const btn = getByTestId('create-bot-submit') as HTMLButtonElement;
    expect(btn).toBeDisabled();
    expect(btn).toHaveTextContent('Creating...');
  });

  it('renders the token reveal panel when newToken is set and copies on click', () => {
    const props = { ...defaultProps(), newToken: 'secret-token-xyz' };
    const { getByTestId, container } = render(() => <CreateBotModal {...props} />);
    expect(container.textContent).toContain('Bot Created');
    expect(container.textContent).toContain('secret-token-xyz');
    fireEvent.click(getByTestId('create-bot-copy-token'));
    expect(props.onCopyToken).toHaveBeenCalledTimes(1);
  });

  it('renders agent select and parameter fields when an agent with manifest is selected', () => {
    const agent = makeAgent({
      manifest: {
        name: 'Echo Agent',
        description: null,
        category: null,
        parameters: [
          {
            name: 'apiKey',
            type: 'string',
            description: null,
            required: true,
            defaultValue: null,
          },
        ],
      },
    });
    const props = {
      ...defaultProps(),
      agents: [agent],
      agentId: 'agent-1',
    };
    const { getByTestId, container } = render(() => <CreateBotModal {...props} />);
    expect(getByTestId('create-bot-agent-select')).toBeInTheDocument();
    expect(container.textContent).toContain('Agent Parameters');
    expect(container.textContent).toContain('apiKey');
  });

  it('shows the createError alert when set', () => {
    const { getByRole } = render(() => (
      <CreateBotModal {...defaultProps()} createError="Username taken" />
    ));
    expect(getByRole('alert')).toHaveTextContent('Username taken');
  });
});
