import type { Setter } from 'solid-js';
import { api } from '../../api/client';
import { getErrorMessage } from '../../utils/errors';

export interface BotActionHandlersDeps {
  setActionBotId: Setter<string | null>;
  setActionError: Setter<string | null>;
  loadBots: () => Promise<void>;
}

/**
 * Handlers for one-shot bot actions: start, stop, revoke token, delete.
 * All share `actionBotId` (which row is busy) and `actionError`.
 */
export function createBotActionHandlers(deps: BotActionHandlersDeps) {
  const { setActionBotId, setActionError, loadBots } = deps;

  async function handleStartBot(botId: string) {
    setActionBotId(botId);
    setActionError(null);
    try {
      await api.post(`/api/v1/admin/bots/${botId}/start`);
      await loadBots();
    } catch (err: unknown) {
      setActionError(getErrorMessage(err, 'Failed to start bot.'));
    } finally {
      setActionBotId(null);
    }
  }

  async function handleStopBot(botId: string) {
    setActionBotId(botId);
    setActionError(null);
    try {
      await api.post(`/api/v1/admin/bots/${botId}/stop`);
      await loadBots();
    } catch (err: unknown) {
      setActionError(getErrorMessage(err, 'Failed to stop bot.'));
    } finally {
      setActionBotId(null);
    }
  }

  async function handleRevokeToken(botId: string, tokenId: string) {
    setActionBotId(botId);
    setActionError(null);
    try {
      await api.delete(`/api/v1/admin/bots/${botId}/tokens/${tokenId}`);
      await loadBots();
    } catch (err: unknown) {
      setActionError(getErrorMessage(err, 'Failed to revoke token.'));
    } finally {
      setActionBotId(null);
    }
  }

  async function handleDeleteBot(botId: string) {
    if (!confirm('Are you sure you want to delete this bot? This cannot be undone.')) return;
    setActionBotId(botId);
    setActionError(null);
    try {
      await api.delete(`/api/v1/admin/bots/${botId}`);
      await loadBots();
    } catch (err: unknown) {
      setActionError(getErrorMessage(err, 'Failed to delete bot.'));
    } finally {
      setActionBotId(null);
    }
  }

  return {
    handleStartBot,
    handleStopBot,
    handleRevokeToken,
    handleDeleteBot,
  };
}
