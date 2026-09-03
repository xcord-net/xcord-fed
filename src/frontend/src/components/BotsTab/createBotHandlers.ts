import { createSignal, type Accessor } from 'solid-js';
import { api } from '../../api/client';
import { getErrorMessage } from '../../utils/errors';
import type { BotAgent, CreateBotResponse } from './types';
import { buildDefaultParamValues } from './helpers';

export interface CreateBotHandlersDeps {
  agents: Accessor<BotAgent[]>;
  loadBots: () => Promise<void>;
}

/**
 * State + handlers for the "Create Bot" modal flow.
 * Includes username/display-name/token-name fields, optional agent + param
 * defaults, submit, dismiss/reset, and the post-create token reveal.
 */
export function createCreateBotHandlers(deps: CreateBotHandlersDeps) {
  const [showCreateModal, setShowCreateModal] = createSignal(false);
  const [createUsername, setCreateUsername] = createSignal('');
  const [createDisplayName, setCreateDisplayName] = createSignal('');
  const [createTokenName, setCreateTokenName] = createSignal('Bot Token');
  const [createAgentId, setCreateAgentId] = createSignal('');
  const [createParamValues, setCreateParamValues] = createSignal<Record<string, string>>({});
  const [isCreating, setIsCreating] = createSignal(false);
  const [createError, setCreateError] = createSignal<string | null>(null);
  const [newToken, setNewToken] = createSignal<string | null>(null);
  const [copiedToken, setCopiedToken] = createSignal(false);

  function handleCreateAgentChange(agentId: string) {
    setCreateAgentId(agentId);
    if (!agentId) {
      setCreateParamValues({});
      return;
    }
    const agent = deps.agents().find((a) => a.id === agentId);
    if (agent?.manifest?.parameters) {
      setCreateParamValues(buildDefaultParamValues(agent.manifest.parameters));
    } else {
      setCreateParamValues({});
    }
  }

  async function handleCreateBot(e: Event) {
    e.preventDefault();
    setIsCreating(true);
    setCreateError(null);
    try {
      const result = await api.post<CreateBotResponse>('/api/v1/admin/bots', {
        username: createUsername().trim(),
        displayName: createDisplayName().trim(),
        tokenName: createTokenName().trim(),
      });
      // If agent selected, assign it now
      if (createAgentId()) {
        await api.post(`/api/v1/admin/bots/${result.userId}/assign-agent`, {
          agentId: createAgentId(),
          configJson: JSON.stringify(createParamValues()),
        });
      }
      setNewToken(result.rawToken);
      await deps.loadBots();
    } catch (err: unknown) {
      setCreateError(getErrorMessage(err, 'Failed to create bot.'));
    } finally {
      setIsCreating(false);
    }
  }

  function handleDismissCreateModal() {
    setShowCreateModal(false);
    setNewToken(null);
    setCreateUsername('');
    setCreateDisplayName('');
    setCreateTokenName('Bot Token');
    setCreateAgentId('');
    setCreateParamValues({});
    setCreateError(null);
    setCopiedToken(false);
  }

  async function handleCopyToken() {
    const token = newToken();
    if (!token) return;
    await navigator.clipboard.writeText(token);
    setCopiedToken(true);
    setTimeout(() => setCopiedToken(false), 2000);
  }

  return {
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
    handleCreateAgentChange,
    handleCreateBot,
    handleDismissCreateModal,
    handleCopyToken,
  };
}
