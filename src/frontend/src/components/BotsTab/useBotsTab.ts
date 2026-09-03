import { createSignal, onMount } from 'solid-js';
import { api } from '../../api/client';
import { getErrorMessage } from '../../utils/errors';
import type { Bot, BotAgent, BotTokenRow } from './types';
import { createBotActionHandlers } from './botActionHandlers';
import { createCreateBotHandlers } from './createBotHandlers';
import { createConfigureBotHandlers } from './configureBotHandlers';
import { createAssignAgentHandlers } from './assignAgentHandlers';

/**
 * Holds all state and async handlers for the BotsTab tree.
 * Sub-handlers are split into focused modules to keep this file small.
 */
export function useBotsTab() {
  const [bots, setBots] = createSignal<Bot[]>([]);
  const [agents, setAgents] = createSignal<BotAgent[]>([]);
  const [isLoadingBots, setIsLoadingBots] = createSignal(true);
  const [isLoadingAgents, setIsLoadingAgents] = createSignal(true);
  const [loadError, setLoadError] = createSignal<string | null>(null);

  // Action state shared by start/stop/delete/revoke handlers
  const [actionBotId, setActionBotId] = createSignal<string | null>(null);
  const [actionError, setActionError] = createSignal<string | null>(null);

  // Assign-agent state shared between bot row and agent row
  const [assignAgentBotId, setAssignAgentBotId] = createSignal<string | null>(null);
  const [assignAgentId, setAssignAgentId] = createSignal('');
  const [isAssigning, setIsAssigning] = createSignal(false);
  const [assignError, setAssignError] = createSignal<string | null>(null);

  onMount(async () => {
    await Promise.all([loadBots(), loadAgents()]);
  });

  async function loadBots() {
    setIsLoadingBots(true);
    try {
      // The endpoint answers `{ bots: [...] }`, and each entry is a *token* -
      // one row per token, carrying the bot it belongs to. Reading it as a bare
      // array of bots left the list empty however many bots existed, because a
      // `For` over an object renders nothing at all.
      const data = await api.get<{ bots?: BotTokenRow[] } | BotTokenRow[]>('/api/v1/admin/bots');
      const rows: BotTokenRow[] = Array.isArray(data) ? data : (data?.bots ?? []);

      const byUser = new Map<string, Bot>();
      for (const row of rows) {
        let bot = byUser.get(row.userId);
        if (!bot) {
          bot = {
            id: row.userId,
            username: row.username,
            // The listing carries no display name; the username is what a bot is
            // actually known by, so it stands in rather than leaving a blank row.
            displayName: row.displayName ?? row.username,
            agentId: null,
            agentName: null,
            isRunning: false,
            agentConfigJson: null,
            tokens: [],
          };
          byUser.set(row.userId, bot);
        }
        bot.tokens.push({
          id: row.tokenId,
          name: row.tokenName,
          tokenHash: row.tokenHash ?? '',
          createdAt: row.createdAt,
          lastUsedAt: row.lastUsedAt ?? null,
        });
      }
      setBots([...byUser.values()]);
    } catch (err: unknown) {
      setLoadError(getErrorMessage(err, 'Failed to load bots.'));
    } finally {
      setIsLoadingBots(false);
    }
  }

  async function loadAgents() {
    setIsLoadingAgents(true);
    try {
      const data = await api.get<BotAgent[]>('/api/v1/admin/bots/agents');
      setAgents(data ?? []);
    } catch {
      // Agents section failing is non-fatal; show empty list
      setAgents([]);
    } finally {
      setIsLoadingAgents(false);
    }
  }

  const createHandlers = createCreateBotHandlers({ agents, loadBots });
  const configureHandlers = createConfigureBotHandlers({ agents, loadBots });
  const actionHandlers = createBotActionHandlers({
    setActionBotId,
    setActionError,
    loadBots,
  });
  const assignHandlers = createAssignAgentHandlers({
    setAssignAgentBotId,
    assignAgentId,
    setAssignAgentId,
    setIsAssigning,
    setAssignError,
    loadBots,
  });

  const unassignedBots = () => bots().filter((b) => !b.agentId);

  return {
    // Data accessors
    bots,
    agents,
    isLoadingBots,
    isLoadingAgents,
    loadError,
    unassignedBots,

    // Action state
    actionBotId,
    actionError,

    // Assign agent state
    assignAgentBotId,
    setAssignAgentBotId,
    assignAgentId,
    setAssignAgentId,
    isAssigning,
    assignError,

    // Create flow
    showCreateModal: createHandlers.showCreateModal,
    setShowCreateModal: createHandlers.setShowCreateModal,
    createUsername: createHandlers.createUsername,
    setCreateUsername: createHandlers.setCreateUsername,
    createDisplayName: createHandlers.createDisplayName,
    setCreateDisplayName: createHandlers.setCreateDisplayName,
    createTokenName: createHandlers.createTokenName,
    setCreateTokenName: createHandlers.setCreateTokenName,
    createAgentId: createHandlers.createAgentId,
    createParamValues: createHandlers.createParamValues,
    setCreateParamValues: createHandlers.setCreateParamValues,
    isCreating: createHandlers.isCreating,
    createError: createHandlers.createError,
    newToken: createHandlers.newToken,
    copiedToken: createHandlers.copiedToken,
    handleCreateAgentChange: createHandlers.handleCreateAgentChange,
    handleCreateBot: createHandlers.handleCreateBot,
    handleDismissCreateModal: createHandlers.handleDismissCreateModal,
    handleCopyToken: createHandlers.handleCopyToken,

    // Configure flow
    configuringBot: configureHandlers.configuringBot,
    setConfiguringBot: configureHandlers.setConfiguringBot,
    configParamValues: configureHandlers.configParamValues,
    setConfigParamValues: configureHandlers.setConfigParamValues,
    isSavingConfig: configureHandlers.isSavingConfig,
    configError: configureHandlers.configError,
    configSuccess: configureHandlers.configSuccess,
    configuredBotAgent: configureHandlers.configuredBotAgent,
    openConfigureModal: configureHandlers.openConfigureModal,
    handleSaveConfig: configureHandlers.handleSaveConfig,

    // Bot action handlers
    handleStartBot: actionHandlers.handleStartBot,
    handleStopBot: actionHandlers.handleStopBot,
    handleRevokeToken: actionHandlers.handleRevokeToken,
    handleDeleteBot: actionHandlers.handleDeleteBot,

    // Assign-agent handlers
    handleOpenAssignAgent: assignHandlers.handleOpenAssignAgent,
    handleAssignAgent: assignHandlers.handleAssignAgent,
    handleOpenAssignToBot: assignHandlers.handleOpenAssignToBot,
    handleConfirmAssignToBot: assignHandlers.handleConfirmAssignToBot,
  };
}
