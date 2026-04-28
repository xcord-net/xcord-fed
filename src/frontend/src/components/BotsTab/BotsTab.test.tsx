import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import { createSignal } from 'solid-js';
import type { Bot, BotAgent } from './types';

// ---- Mocks ----
// Mock the useBotsTab composable so we can drive the parent's render branches
// without touching network/api/auth.
type BotsTabMock = ReturnType<typeof makeBotsTabMock>;
function makeBotsTabMock() {
  const [bots, setBots] = createSignal<Bot[]>([]);
  const [agents, setAgents] = createSignal<BotAgent[]>([]);
  const [isLoadingBots, setIsLoadingBots] = createSignal(false);
  const [isLoadingAgents, setIsLoadingAgents] = createSignal(false);
  const [loadError, setLoadError] = createSignal<string | null>(null);
  const [actionError, setActionError] = createSignal<string | null>(null);
  const [actionBotId] = createSignal<string | null>(null);
  const [assignAgentBotId, setAssignAgentBotId] = createSignal<string | null>(null);
  const [assignAgentId, setAssignAgentId] = createSignal('');
  const [isAssigning] = createSignal(false);
  const [assignError] = createSignal<string | null>(null);
  const [showCreateModal, setShowCreateModal] = createSignal(false);
  const [createUsername, setCreateUsername] = createSignal('');
  const [createDisplayName, setCreateDisplayName] = createSignal('');
  const [createTokenName, setCreateTokenName] = createSignal('');
  const [createAgentId] = createSignal('');
  const [createParamValues, setCreateParamValues] = createSignal<Record<string, string>>({});
  const [isCreating] = createSignal(false);
  const [createError] = createSignal<string | null>(null);
  const [newToken] = createSignal<string | null>(null);
  const [copiedToken] = createSignal(false);
  const [configuringBot, setConfiguringBot] = createSignal<Bot | null>(null);
  const [configParamValues, setConfigParamValues] = createSignal<Record<string, string>>({});
  const [isSavingConfig] = createSignal(false);
  const [configError] = createSignal<string | null>(null);
  const [configSuccess] = createSignal<string | null>(null);
  const [configuredBotAgent] = createSignal<BotAgent | null>(null);

  const unassignedBots = () => bots().filter((b) => !b.agentId);

  return {
    // Data
    bots,
    setBots,
    agents,
    setAgents,
    isLoadingBots,
    setIsLoadingBots,
    isLoadingAgents,
    setIsLoadingAgents,
    loadError,
    setLoadError,
    unassignedBots,

    // Action state
    actionBotId,
    actionError,
    setActionError,

    // Assign state
    assignAgentBotId,
    setAssignAgentBotId,
    assignAgentId,
    setAssignAgentId,
    isAssigning,
    assignError,

    // Create flow
    showCreateModal,
    setShowCreateModal,
    createUsername,
    setCreateUsername,
    createDisplayName,
    setCreateDisplayName,
    createTokenName,
    setCreateTokenName,
    createAgentId,
    createParamValues,
    setCreateParamValues,
    isCreating,
    createError,
    newToken,
    copiedToken,
    handleCreateAgentChange: vi.fn(),
    handleCreateBot: vi.fn(),
    handleDismissCreateModal: vi.fn(() => setShowCreateModal(false)),
    handleCopyToken: vi.fn(),

    // Configure flow
    configuringBot,
    setConfiguringBot,
    configParamValues,
    setConfigParamValues,
    isSavingConfig,
    configError,
    configSuccess,
    configuredBotAgent,
    openConfigureModal: vi.fn((bot: Bot) => setConfiguringBot(bot)),
    handleSaveConfig: vi.fn(),

    // Action handlers
    handleStartBot: vi.fn(),
    handleStopBot: vi.fn(),
    handleRevokeToken: vi.fn(),
    handleDeleteBot: vi.fn(),

    // Assign handlers
    handleOpenAssignAgent: vi.fn(),
    handleAssignAgent: vi.fn(),
    handleOpenAssignToBot: vi.fn(),
    handleConfirmAssignToBot: vi.fn(),
  };
}

let botsTabMock: BotsTabMock;
vi.mock('./useBotsTab', () => ({
  useBotsTab: () => botsTabMock,
}));

// Stub heavy children so the parent test focuses on its own composition logic.
vi.mock('./BotList', () => ({
  BotList: (p: { onCreate: () => void }) => (
    <div data-testid="mock-bot-list">
      <button data-testid="mock-bot-list-create" onClick={p.onCreate}>
        create
      </button>
    </div>
  ),
}));
vi.mock('./AgentList', () => ({
  AgentList: () => <div data-testid="mock-agent-list" />,
}));
vi.mock('./CreateBotModal', () => ({
  CreateBotModal: () => <div data-testid="mock-create-bot-modal" />,
}));
vi.mock('./ConfigureBotModal', () => ({
  ConfigureBotModal: (p: { bot: Bot }) => (
    <div data-testid="mock-configure-bot-modal">{p.bot.displayName}</div>
  ),
}));

// Import after mocks are set up.
import BotsTab from './BotsTab';

describe('BotsTab', () => {
  beforeEach(() => {
    botsTabMock = makeBotsTabMock();
  });

  it('renders the tab container with both list sections', () => {
    const { getByTestId } = render(() => <BotsTab />);
    expect(getByTestId('bots-tab')).toBeInTheDocument();
    expect(getByTestId('mock-bot-list')).toBeInTheDocument();
    expect(getByTestId('mock-agent-list')).toBeInTheDocument();
  });

  it('does not render the create modal by default', () => {
    const { queryByTestId } = render(() => <BotsTab />);
    expect(queryByTestId('mock-create-bot-modal')).toBeNull();
  });

  it('renders the create modal when showCreateModal is true', () => {
    botsTabMock.setShowCreateModal(true);
    const { getByTestId } = render(() => <BotsTab />);
    expect(getByTestId('mock-create-bot-modal')).toBeInTheDocument();
  });

  it('opens the create modal when the BotList onCreate callback fires', () => {
    const { getByTestId, queryByTestId } = render(() => <BotsTab />);
    expect(queryByTestId('mock-create-bot-modal')).toBeNull();
    fireEvent.click(getByTestId('mock-bot-list-create'));
    expect(getByTestId('mock-create-bot-modal')).toBeInTheDocument();
  });

  it('renders the configure modal only when configuringBot is set', () => {
    const { queryByTestId, getByTestId } = render(() => <BotsTab />);
    expect(queryByTestId('mock-configure-bot-modal')).toBeNull();
    botsTabMock.setConfiguringBot({
      id: 'bot-9',
      username: 'b',
      displayName: 'Configurable Bot',
      agentId: 'agent-1',
      agentName: 'Echo',
      isRunning: false,
      agentConfigJson: null,
      tokens: [],
    });
    expect(getByTestId('mock-configure-bot-modal')).toHaveTextContent('Configurable Bot');
  });

  it('renders the loadError alert when loadError is set', () => {
    botsTabMock.setLoadError('Failed to load bots.');
    const { getByRole } = render(() => <BotsTab />);
    expect(getByRole('alert')).toHaveTextContent('Failed to load bots.');
  });

  it('renders the actionError alert when actionError is set', () => {
    botsTabMock.setActionError('Could not start bot.');
    const { getByRole } = render(() => <BotsTab />);
    expect(getByRole('alert')).toHaveTextContent('Could not start bot.');
  });
});
