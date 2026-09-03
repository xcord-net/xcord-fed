import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import { AgentList } from './AgentList';
import type { Bot, BotAgent } from './types';

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

function defaultProps() {
  return {
    agents: [] as BotAgent[],
    isLoading: false,
    unassignedBots: [] as Bot[],
    assignAgentBotId: null as string | null,
    isAssigning: false,
    assignError: null as string | null,
    onOpenAssignToBot: vi.fn(),
    onSetAssignAgentBotIdRaw: vi.fn(),
    onCancelAssignToBot: vi.fn(),
    onConfirmAssignToBot: vi.fn(),
  };
}

describe('AgentList', () => {
  it('renders without crashing with minimal props', () => {
    const { container } = render(() => <AgentList {...defaultProps()} />);
    expect(container.textContent).toContain('Available Agents');
  });

  it('renders the empty state when no agents and not loading', () => {
    const { getByTestId } = render(() => <AgentList {...defaultProps()} />);
    expect(getByTestId('agents-empty-state')).toHaveTextContent('No agents available');
  });

  it('does not render the empty state while loading', () => {
    const { queryByTestId } = render(() => (
      <AgentList {...defaultProps()} isLoading={true} />
    ));
    expect(queryByTestId('agents-empty-state')).toBeNull();
  });

  it('renders a card for each agent with name and category', () => {
    const props = {
      ...defaultProps(),
      agents: [
        makeAgent({ id: 'agent-1', name: 'Echo Agent', category: 'utility' }),
        makeAgent({ id: 'agent-2', name: 'Translator' }),
      ],
    };
    const { getByTestId, container } = render(() => <AgentList {...props} />);
    expect(getByTestId('agent-card-agent-1')).toBeInTheDocument();
    expect(getByTestId('agent-card-agent-2')).toBeInTheDocument();
    expect(container.textContent).toContain('utility');
  });

  it('shows Assign-to-Bot button only when there are unassigned bots and triggers the callback', () => {
    const props = {
      ...defaultProps(),
      agents: [makeAgent()],
      unassignedBots: [makeBot()],
    };
    const { getByTestId } = render(() => <AgentList {...props} />);
    fireEvent.click(getByTestId('agent-assign-agent-1'));
    expect(props.onOpenAssignToBot).toHaveBeenCalledWith('agent-1');
  });

  it('renders the inline assign row when assignAgentBotId matches the agent key', () => {
    const props = {
      ...defaultProps(),
      agents: [makeAgent()],
      unassignedBots: [makeBot()],
      assignAgentBotId: 'agent-1-agent',
    };
    const { getByTestId } = render(() => <AgentList {...props} />);
    expect(getByTestId('agent-assign-bot-select-agent-1')).toBeInTheDocument();
    fireEvent.click(getByTestId('agent-assign-confirm-agent-1'));
    expect(props.onConfirmAssignToBot).toHaveBeenCalledWith('agent-1');
  });

  it('shows the assign error within the inline row when present', () => {
    const props = {
      ...defaultProps(),
      agents: [makeAgent()],
      unassignedBots: [makeBot()],
      assignAgentBotId: 'agent-1-agent',
      assignError: 'Bot already assigned',
    };
    const { container } = render(() => <AgentList {...props} />);
    expect(container.textContent).toContain('Bot already assigned');
  });
});
