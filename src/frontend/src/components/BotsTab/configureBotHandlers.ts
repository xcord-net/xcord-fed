import { createSignal, type Accessor } from 'solid-js';
import { api } from '../../api/client';
import { getErrorMessage } from '../../utils/errors';
import type { Bot, BotAgent } from './types';
import { buildDefaultParamValues, parseAgentConfig } from './helpers';

export interface ConfigureBotHandlersDeps {
  agents: Accessor<BotAgent[]>;
  loadBots: () => Promise<void>;
}

/**
 * State + handlers for the per-bot "Configure" modal that edits the agent
 * parameter config JSON.
 */
export function createConfigureBotHandlers(deps: ConfigureBotHandlersDeps) {
  const [configuringBot, setConfiguringBot] = createSignal<Bot | null>(null);
  const [configParamValues, setConfigParamValues] = createSignal<Record<string, string>>({});
  const [isSavingConfig, setIsSavingConfig] = createSignal(false);
  const [configError, setConfigError] = createSignal<string | null>(null);
  const [configSuccess, setConfigSuccess] = createSignal<string | null>(null);

  function openConfigureModal(bot: Bot) {
    setConfiguringBot(bot);
    setConfigError(null);
    setConfigSuccess(null);
    const agent = deps.agents().find((a) => a.id === bot.agentId);
    const current = parseAgentConfig(bot.agentConfigJson);
    if (agent?.manifest?.parameters) {
      const defaults = buildDefaultParamValues(agent.manifest.parameters);
      setConfigParamValues({ ...defaults, ...current });
    } else {
      setConfigParamValues(current);
    }
  }

  function configuredBotAgent(): BotAgent | null {
    const bot = configuringBot();
    if (!bot?.agentId) return null;
    return deps.agents().find((a) => a.id === bot.agentId) ?? null;
  }

  async function handleSaveConfig(e: Event) {
    e.preventDefault();
    const bot = configuringBot();
    if (!bot) return;
    setIsSavingConfig(true);
    setConfigError(null);
    setConfigSuccess(null);
    try {
      await api.patch(`/api/v1/admin/bots/${bot.id}/agent-config`, {
        configJson: JSON.stringify(configParamValues()),
      });
      setConfigSuccess('Configuration saved.');
      await deps.loadBots();
    } catch (err: unknown) {
      setConfigError(getErrorMessage(err, 'Failed to save configuration.'));
    } finally {
      setIsSavingConfig(false);
    }
  }

  return {
    configuringBot,
    setConfiguringBot,
    configParamValues,
    setConfigParamValues,
    isSavingConfig,
    configError,
    configSuccess,
    configuredBotAgent,
    openConfigureModal,
    handleSaveConfig,
  };
}
