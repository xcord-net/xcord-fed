import type { Accessor, Setter } from 'solid-js';
import { api } from '../../api/client';
import { getErrorMessage } from '../../utils/errors';

export interface AssignAgentHandlersDeps {
  setAssignAgentBotId: Setter<string | null>;
  assignAgentId: Accessor<string>;
  setAssignAgentId: Setter<string>;
  setIsAssigning: Setter<boolean>;
  setAssignError: Setter<string | null>;
  loadBots: () => Promise<void>;
}

/**
 * Handlers for assigning agents to bots in two flows:
 *  - bot row -> "Assign Agent" dropdown that picks an agent to attach
 *  - agent row -> "Assign to Bot" dropdown that picks an unassigned bot
 *
 * The agent-row confirm reads the selected bot id from the DOM via
 * `data-testid="agent-assign-bot-select-{agentId}"` to preserve the original
 * BotsTab behavior exactly.
 */
export function createAssignAgentHandlers(deps: AssignAgentHandlersDeps) {
  function handleOpenAssignAgent(botId: string) {
    deps.setAssignAgentBotId(botId);
    deps.setAssignAgentId('');
    deps.setAssignError(null);
  }

  async function handleAssignAgent(botId: string) {
    if (!deps.assignAgentId()) return;
    deps.setIsAssigning(true);
    deps.setAssignError(null);
    try {
      await api.post(`/api/v1/admin/bots/${botId}/assign-agent`, {
        agentId: deps.assignAgentId(),
        configJson: '{}',
      });
      deps.setAssignAgentBotId(null);
      await deps.loadBots();
    } catch (err: unknown) {
      deps.setAssignError(getErrorMessage(err, 'Failed to assign agent.'));
    } finally {
      deps.setIsAssigning(false);
    }
  }

  function handleOpenAssignToBot(agentId: string) {
    deps.setAssignAgentBotId(agentId + '-agent');
    deps.setAssignAgentId(agentId);
    deps.setAssignError(null);
  }

  async function handleConfirmAssignToBot(agentId: string) {
    const sel = document.querySelector<HTMLSelectElement>(
      `[data-testid="agent-assign-bot-select-${agentId}"]`,
    );
    const botId = sel?.value;
    if (!botId) return;
    deps.setIsAssigning(true);
    deps.setAssignError(null);
    try {
      await api.post(`/api/v1/admin/bots/${botId}/assign-agent`, {
        agentId,
        configJson: '{}',
      });
      deps.setAssignAgentBotId(null);
      await deps.loadBots();
    } catch (err: unknown) {
      deps.setAssignError(getErrorMessage(err, 'Failed to assign agent.'));
    } finally {
      deps.setIsAssigning(false);
    }
  }

  return {
    handleOpenAssignAgent,
    handleAssignAgent,
    handleOpenAssignToBot,
    handleConfirmAssignToBot,
  };
}
