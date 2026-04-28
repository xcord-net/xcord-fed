import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import { BotCard } from './BotCard';
import type { Bot, BotAgent } from './types';

function makeBot(over: Partial<Bot> = {}): Bot {
  return {
    id: 'bot-1',
    username: 'helperbot',
    displayName: 'Helper Bot',
    agentId: null,
    agentName: null,
    isRunning: false,
    agentConfigJson: null,
    tokens: [],
    ...over,
  };
}

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
    bot: makeBot(),
    agents: [] as BotAgent[],
    actionBotId: null as string | null,
    assignAgentBotId: null as string | null,
    assignAgentId: '',
    isAssigning: false,
    assignError: null as string | null,
    onSetAssignAgentId: vi.fn(),
    onOpenAssignAgent: vi.fn(),
    onCancelAssignAgent: vi.fn(),
    onConfirmAssignAgent: vi.fn(),
    onConfigure: vi.fn(),
    onStart: vi.fn(),
    onStop: vi.fn(),
    onDelete: vi.fn(),
    onRevokeToken: vi.fn(),
  };
}

describe('BotCard', () => {
  it('renders without crashing with minimal valid props', () => {
    const { getByTestId } = render(() => <BotCard {...defaultProps()} />);
    expect(getByTestId('bot-card-bot-1')).toBeInTheDocument();
  });

  it('shows display name, username, and unassigned agent text', () => {
    const { container } = render(() => <BotCard {...defaultProps()} />);
    expect(container.textContent).toContain('Helper Bot');
    expect(container.textContent).toContain('@helperbot');
    expect(container.textContent).toContain('No agent assigned');
  });

  it('shows Assign Agent button when no agent is assigned and triggers callback', () => {
    const props = defaultProps();
    const { getByTestId } = render(() => <BotCard {...props} />);
    fireEvent.click(getByTestId('bot-assign-agent-bot-1'));
    expect(props.onOpenAssignAgent).toHaveBeenCalledWith('bot-1');
  });

  it('shows Start button when an agent is assigned and not running, triggers onStart', () => {
    const props = {
      ...defaultProps(),
      bot: makeBot({ agentId: 'agent-1', agentName: 'Echo Agent', isRunning: false }),
    };
    const { getByTestId, queryByTestId } = render(() => <BotCard {...props} />);
    expect(queryByTestId('bot-stop-bot-1')).toBeNull();
    fireEvent.click(getByTestId('bot-start-bot-1'));
    expect(props.onStart).toHaveBeenCalledWith('bot-1');
  });

  it('shows Stop button when bot is running, triggers onStop', () => {
    const props = {
      ...defaultProps(),
      bot: makeBot({ agentId: 'agent-1', agentName: 'Echo Agent', isRunning: true }),
    };
    const { getByTestId } = render(() => <BotCard {...props} />);
    fireEvent.click(getByTestId('bot-stop-bot-1'));
    expect(props.onStop).toHaveBeenCalledWith('bot-1');
  });

  it('renders the assign-agent inline form with options when assignAgentBotId matches', () => {
    const props = {
      ...defaultProps(),
      agents: [makeAgent(), makeAgent({ id: 'agent-2', name: 'Voice Agent' })],
      assignAgentBotId: 'bot-1',
    };
    const { getByTestId, container } = render(() => <BotCard {...props} />);
    const select = getByTestId('bot-assign-select-bot-1') as HTMLSelectElement;
    expect(select).toBeInTheDocument();
    expect(container.textContent).toContain('Echo Agent');
    expect(container.textContent).toContain('Voice Agent');
  });

  it('renders token rows and triggers onRevokeToken when Revoke is clicked', () => {
    const props = {
      ...defaultProps(),
      bot: makeBot({
        tokens: [
          {
            id: 'tok-1',
            name: 'main token',
            tokenHash: 'abcdef0123456789xxxxxx',
            createdAt: '2024-01-01T00:00:00Z',
            lastUsedAt: null,
          },
        ],
      }),
    };
    const { getByTestId, container } = render(() => <BotCard {...props} />);
    expect(container.textContent).toContain('main token');
    fireEvent.click(getByTestId('bot-revoke-token-tok-1'));
    expect(props.onRevokeToken).toHaveBeenCalledWith('bot-1', 'tok-1');
  });
});
