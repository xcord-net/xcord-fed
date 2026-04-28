import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import { BotList } from './BotList';
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

function defaultProps() {
  return {
    bots: [] as Bot[],
    agents: [] as BotAgent[],
    isLoading: false,
    actionBotId: null as string | null,
    assignAgentBotId: null as string | null,
    assignAgentId: '',
    isAssigning: false,
    assignError: null as string | null,
    onCreate: vi.fn(),
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

describe('BotList', () => {
  it('renders without crashing with minimal props', () => {
    const { getByTestId } = render(() => <BotList {...defaultProps()} />);
    expect(getByTestId('bots-create-button')).toBeInTheDocument();
  });

  it('renders the section title', () => {
    const { container } = render(() => <BotList {...defaultProps()} />);
    expect(container.textContent).toContain('Your Bots');
  });

  it('shows the empty state when not loading and bots is empty', () => {
    const { getByTestId } = render(() => <BotList {...defaultProps()} />);
    expect(getByTestId('bots-empty-state')).toHaveTextContent(
      'No bots yet. Create one to get started.',
    );
  });

  it('hides the empty state while loading', () => {
    const { queryByTestId } = render(() => (
      <BotList {...defaultProps()} isLoading={true} />
    ));
    expect(queryByTestId('bots-empty-state')).toBeNull();
  });

  it('invokes onCreate when the Create Bot button is clicked', () => {
    const props = defaultProps();
    const { getByTestId } = render(() => <BotList {...props} />);
    fireEvent.click(getByTestId('bots-create-button'));
    expect(props.onCreate).toHaveBeenCalledTimes(1);
  });

  it('renders a BotCard for each bot when not loading', () => {
    const props = {
      ...defaultProps(),
      bots: [
        makeBot({ id: 'bot-1', displayName: 'A Bot' }),
        makeBot({ id: 'bot-2', displayName: 'B Bot', username: 'b' }),
      ],
    };
    const { getByTestId } = render(() => <BotList {...props} />);
    expect(getByTestId('bot-card-bot-1')).toBeInTheDocument();
    expect(getByTestId('bot-card-bot-2')).toBeInTheDocument();
  });
});
