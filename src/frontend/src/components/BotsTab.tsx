import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './BotsTab.module.css';

// ---- Types ----

interface AgentParameterManifest {
  name: string;
  type: 'string' | 'number' | 'boolean';
  description: string | null;
  required: boolean;
  defaultValue: string | null;
}

interface AgentManifest {
  name: string;
  description: string | null;
  category: string | null;
  parameters: AgentParameterManifest[];
}

interface BotAgent {
  id: string;
  name: string;
  description: string | null;
  category: string | null;
  manifest: AgentManifest | null;
}

interface BotToken {
  id: string;
  name: string;
  tokenHash: string;
  createdAt: string;
  lastUsedAt: string | null;
}

interface Bot {
  id: string;
  username: string;
  displayName: string;
  agentId: string | null;
  agentName: string | null;
  isRunning: boolean;
  agentConfigJson: string | null;
  tokens: BotToken[];
}

interface CreateBotResponse {
  id: string;
  username: string;
  displayName: string;
  token: string;
  tokenId: string;
}

// ---- Helpers ----

function formatDate(dateStr: string | null): string {
  if (!dateStr) return '-';
  return new Date(dateStr).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function parseAgentConfig(json: string | null): Record<string, string> {
  if (!json) return {};
  try {
    return JSON.parse(json) as Record<string, string>;
  } catch {
    return {};
  }
}

function buildDefaultParamValues(params: AgentParameterManifest[]): Record<string, string> {
  const result: Record<string, string> = {};
  for (const p of params) {
    result[p.name] = p.defaultValue ?? '';
  }
  return result;
}

// ---- Parameter field component ----

interface ParamFieldProps {
  param: AgentParameterManifest;
  value: string;
  onChange: (val: string) => void;
}

function ParamField(props: ParamFieldProps) {
  return (
    <div class={styles.paramField}>
      <label class={styles.paramLabel}>
        {props.param.name}
        {props.param.required && <span class={styles.paramRequiredMark}>*</span>}
      </label>
      <Show when={props.param.description}>
        <p class={styles.paramDescription}>{props.param.description}</p>
      </Show>
      <Show when={props.param.type === 'boolean'}>
        <label class={styles.paramCheckboxLabel}>
          <input
            type="checkbox"
            checked={props.value === 'true'}
            onChange={(e) => props.onChange(e.currentTarget.checked ? 'true' : 'false')}
            class={styles.paramCheckbox}
          />
          <span class={styles.paramCheckboxText}>Enabled</span>
        </label>
      </Show>
      <Show when={props.param.type === 'number'}>
        <input
          type="number"
          value={props.value}
          onInput={(e) => props.onChange(e.currentTarget.value)}
          class={styles.paramInput}
          required={props.param.required}
        />
      </Show>
      <Show when={props.param.type === 'string'}>
        <input
          type="text"
          value={props.value}
          onInput={(e) => props.onChange(e.currentTarget.value)}
          class={styles.paramInput}
          required={props.param.required}
        />
      </Show>
    </div>
  );
}

// ---- Main component ----

export default function BotsTab() {
  const [bots, setBots] = createSignal<Bot[]>([]);
  const [agents, setAgents] = createSignal<BotAgent[]>([]);
  const [isLoadingBots, setIsLoadingBots] = createSignal(true);
  const [isLoadingAgents, setIsLoadingAgents] = createSignal(true);
  const [loadError, setLoadError] = createSignal<string | null>(null);

  // Create bot modal state
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

  // Configure modal state
  const [configuringBot, setConfiguringBot] = createSignal<Bot | null>(null);
  const [configParamValues, setConfigParamValues] = createSignal<Record<string, string>>({});
  const [isSavingConfig, setIsSavingConfig] = createSignal(false);
  const [configError, setConfigError] = createSignal<string | null>(null);
  const [configSuccess, setConfigSuccess] = createSignal<string | null>(null);

  // Assign agent dropdown state per bot
  const [assignAgentBotId, setAssignAgentBotId] = createSignal<string | null>(null);
  const [assignAgentId, setAssignAgentId] = createSignal('');
  const [isAssigning, setIsAssigning] = createSignal(false);
  const [assignError, setAssignError] = createSignal<string | null>(null);

  // Action state
  const [actionBotId, setActionBotId] = createSignal<string | null>(null);
  const [actionError, setActionError] = createSignal<string | null>(null);

  onMount(async () => {
    await Promise.all([loadBots(), loadAgents()]);
  });

  async function loadBots() {
    setIsLoadingBots(true);
    try {
      const data = await api.get<Bot[]>('/api/v1/admin/bots');
      setBots(data ?? []);
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

  // When selected agent changes in create modal, update param defaults
  function handleCreateAgentChange(agentId: string) {
    setCreateAgentId(agentId);
    if (!agentId) {
      setCreateParamValues({});
      return;
    }
    const agent = agents().find((a) => a.id === agentId);
    if (agent?.manifest?.parameters) {
      setCreateParamValues(buildDefaultParamValues(agent.manifest.parameters));
    } else {
      setCreateParamValues({});
    }
  }

  function selectedCreateAgent() {
    const id = createAgentId();
    if (!id) return null;
    return agents().find((a) => a.id === id) ?? null;
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
        await api.post(`/api/v1/admin/bots/${result.id}/assign-agent`, {
          agentId: createAgentId(),
          configJson: JSON.stringify(createParamValues()),
        });
      }

      setNewToken(result.token);
      await loadBots();
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

  function openConfigureModal(bot: Bot) {
    setConfiguringBot(bot);
    setConfigError(null);
    setConfigSuccess(null);
    // Find the agent manifest
    const agent = agents().find((a) => a.id === bot.agentId);
    const current = parseAgentConfig(bot.agentConfigJson);
    if (agent?.manifest?.parameters) {
      const defaults = buildDefaultParamValues(agent.manifest.parameters);
      setConfigParamValues({ ...defaults, ...current });
    } else {
      setConfigParamValues(current);
    }
  }

  function configuredBotAgent() {
    const bot = configuringBot();
    if (!bot?.agentId) return null;
    return agents().find((a) => a.id === bot.agentId) ?? null;
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
      await loadBots();
    } catch (err: unknown) {
      setConfigError(getErrorMessage(err, 'Failed to save configuration.'));
    } finally {
      setIsSavingConfig(false);
    }
  }

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

  function handleOpenAssignAgent(botId: string) {
    setAssignAgentBotId(botId);
    setAssignAgentId('');
    setAssignError(null);
  }

  async function handleAssignAgent(botId: string) {
    if (!assignAgentId()) return;
    setIsAssigning(true);
    setAssignError(null);
    try {
      await api.post(`/api/v1/admin/bots/${botId}/assign-agent`, {
        agentId: assignAgentId(),
        configJson: '{}',
      });
      setAssignAgentBotId(null);
      await loadBots();
    } catch (err: unknown) {
      setAssignError(getErrorMessage(err, 'Failed to assign agent.'));
    } finally {
      setIsAssigning(false);
    }
  }

  const unassignedBots = () => bots().filter((b) => !b.agentId);

  return (
    <div data-testid="bots-tab" class={styles.container}>
      {/* Load error */}
      <Show when={loadError()}>
        <div role="alert" class={styles.alertError}>
          {loadError()}
        </div>
      </Show>

      {/* Action error (start/stop/delete/revoke) */}
      <Show when={actionError()}>
        <div role="alert" class={styles.alertError}>
          {actionError()}
        </div>
      </Show>

      {/* ---- Section 1: Your Bots ---- */}
      <section>
        <div class={styles.sectionHeader}>
          <h3 class={styles.sectionTitle}>Your Bots</h3>
          <button
            data-testid="bots-create-button"
            type="button"
            onClick={() => setShowCreateModal(true)}
            class={styles.createBotButton}
          >
            + Create Bot
          </button>
        </div>

        <Show when={isLoadingBots()}>
          <div class={styles.spinnerContainer}>
            <div class={styles.spinner} />
          </div>
        </Show>

        <Show when={!isLoadingBots() && bots().length === 0}>
          <div data-testid="bots-empty-state" class={styles.emptyState}>
            No bots yet. Create one to get started.
          </div>
        </Show>

        <Show when={!isLoadingBots() && bots().length > 0}>
          <div class={styles.cardList}>
            <For each={bots()}>
              {(bot) => (
                <div
                  data-testid={`bot-card-${bot.id}`}
                  class={styles.botCard}
                >
                  <div class={styles.botCardTop}>
                    {/* Bot identity */}
                    <div class={styles.botIdentity}>
                      {/* Running status dot */}
                      <span
                        data-testid={`bot-status-${bot.id}`}
                        title={bot.isRunning ? 'Running' : 'Stopped'}
                        class={bot.isRunning ? styles.statusDotRunning : styles.statusDotStopped}
                      />
                      <div class={styles.botNames}>
                        <p class={styles.botDisplayName}>
                          {bot.displayName}
                        </p>
                        <p class={styles.botUsername}>@{bot.username}</p>
                        <p class={styles.botAgent}>
                          Agent:{' '}
                          <span class={bot.agentName ? styles.botAgentName : styles.botAgentUnassigned}>
                            {bot.agentName ?? 'No agent assigned'}
                          </span>
                        </p>
                      </div>
                    </div>

                    {/* Actions */}
                    <div class={styles.botActions}>
                      {/* Start/Stop - only if agent is assigned */}
                      <Show when={bot.agentId}>
                        <Show when={bot.isRunning}>
                          <button
                            data-testid={`bot-stop-${bot.id}`}
                            type="button"
                            disabled={actionBotId() === bot.id}
                            onClick={() => handleStopBot(bot.id)}
                            class={styles.stopButton}
                          >
                            Stop
                          </button>
                        </Show>
                        <Show when={!bot.isRunning}>
                          <button
                            data-testid={`bot-start-${bot.id}`}
                            type="button"
                            disabled={actionBotId() === bot.id}
                            onClick={() => handleStartBot(bot.id)}
                            class={styles.startButton}
                          >
                            Start
                          </button>
                        </Show>
                      </Show>

                      {/* Configure */}
                      <Show when={bot.agentId}>
                        <button
                          data-testid={`bot-configure-${bot.id}`}
                          type="button"
                          onClick={() => openConfigureModal(bot)}
                          class={styles.configureButton}
                        >
                          Configure
                        </button>
                      </Show>

                      {/* Assign agent dropdown (when no agent) */}
                      <Show when={!bot.agentId}>
                        <Show when={assignAgentBotId() === bot.id}>
                          <div class={styles.assignAgentRow}>
                            <select
                              data-testid={`bot-assign-select-${bot.id}`}
                              value={assignAgentId()}
                              onChange={(e) => setAssignAgentId(e.currentTarget.value)}
                              class={styles.assignSelect}
                            >
                              <option value="">Select agent...</option>
                              <For each={agents()}>
                                {(agent) => (
                                  <option value={agent.id}>{agent.name}</option>
                                )}
                              </For>
                            </select>
                            <button
                              data-testid={`bot-assign-confirm-${bot.id}`}
                              type="button"
                              disabled={!assignAgentId() || isAssigning()}
                              onClick={() => handleAssignAgent(bot.id)}
                              class={styles.assignConfirmButton}
                            >
                              {isAssigning() ? '...' : 'Assign'}
                            </button>
                            <button
                              type="button"
                              onClick={() => setAssignAgentBotId(null)}
                              class={styles.assignCancelButton}
                            >
                              Cancel
                            </button>
                          </div>
                          <Show when={assignError()}>
                            <p class={styles.assignError}>{assignError()}</p>
                          </Show>
                        </Show>
                        <Show when={assignAgentBotId() !== bot.id}>
                          <button
                            data-testid={`bot-assign-agent-${bot.id}`}
                            type="button"
                            onClick={() => handleOpenAssignAgent(bot.id)}
                            class={styles.assignAgentButton}
                          >
                            Assign Agent
                          </button>
                        </Show>
                      </Show>

                      {/* Delete */}
                      <button
                        data-testid={`bot-delete-${bot.id}`}
                        type="button"
                        disabled={actionBotId() === bot.id}
                        onClick={() => handleDeleteBot(bot.id)}
                        class={styles.deleteButton}
                      >
                        Delete
                      </button>
                    </div>
                  </div>

                  {/* Token list */}
                  <Show when={bot.tokens && bot.tokens.length > 0}>
                    <div class={styles.tokenSection}>
                      <p class={styles.tokenSectionLabel}>Tokens</p>
                      <For each={bot.tokens}>
                        {(token) => (
                          <div
                            data-testid={`bot-token-${token.id}`}
                            class={styles.tokenRow}
                          >
                            <div class={styles.tokenInfo}>
                              <p class={styles.tokenName}>{token.name}</p>
                              <p class={styles.tokenHash}>
                                {token.tokenHash.slice(0, 12)}...
                              </p>
                              <div class={styles.tokenDates}>
                                <span>Created: {formatDate(token.createdAt)}</span>
                                <span>Last used: {formatDate(token.lastUsedAt)}</span>
                              </div>
                            </div>
                            <button
                              data-testid={`bot-revoke-token-${token.id}`}
                              type="button"
                              disabled={actionBotId() === bot.id}
                              onClick={() => handleRevokeToken(bot.id, token.id)}
                              class={styles.revokeButton}
                            >
                              Revoke
                            </button>
                          </div>
                        )}
                      </For>
                    </div>
                  </Show>
                </div>
              )}
            </For>
          </div>
        </Show>
      </section>

      {/* ---- Section 2: Available Agents ---- */}
      <section>
        <h3 class={styles.sectionTitle} style="margin-bottom:1rem">Available Agents</h3>

        <Show when={isLoadingAgents()}>
          <div class={styles.spinnerSmall}>
            <div class={styles.spinner} />
          </div>
        </Show>

        <Show when={!isLoadingAgents() && agents().length === 0}>
          <div data-testid="agents-empty-state" class={styles.emptyState}>
            No agents available.
          </div>
        </Show>

        <Show when={!isLoadingAgents() && agents().length > 0}>
          <div class={styles.cardList}>
            <For each={agents()}>
              {(agent) => (
                <div
                  data-testid={`agent-card-${agent.id}`}
                  class={styles.agentCard}
                >
                  <div class={styles.agentCardInner}>
                    <div class={styles.agentInfo}>
                      <div class={styles.agentNameRow}>
                        <p class={styles.agentName}>{agent.name}</p>
                        <Show when={agent.category}>
                          <span class={styles.agentCategory}>
                            {agent.category}
                          </span>
                        </Show>
                      </div>
                      <Show when={agent.description}>
                        <p class={styles.agentDescription}>{agent.description}</p>
                      </Show>
                      <Show when={agent.manifest?.parameters && agent.manifest.parameters.length > 0}>
                        <p class={styles.agentParams}>
                          Parameters:{' '}
                          {agent.manifest!.parameters.map((p) => p.name).join(', ')}
                        </p>
                      </Show>
                    </div>

                    {/* Assign to Bot button */}
                    <Show when={unassignedBots().length > 0}>
                      <Show when={assignAgentBotId() !== agent.id + '-agent'}>
                        <button
                          data-testid={`agent-assign-${agent.id}`}
                          type="button"
                          onClick={() => {
                            setAssignAgentBotId(agent.id + '-agent');
                            setAssignAgentId(agent.id);
                            setAssignError(null);
                          }}
                          class={styles.assignToBotButton}
                        >
                          Assign to Bot
                        </button>
                      </Show>
                      <Show when={assignAgentBotId() === agent.id + '-agent'}>
                        <div class={styles.agentAssignRow}>
                          <select
                            data-testid={`agent-assign-bot-select-${agent.id}`}
                            onChange={(e) => setAssignAgentBotId(e.currentTarget.value ? agent.id + '-agent' : null)}
                            class={styles.assignSelect}
                          >
                            <option value="">Select bot...</option>
                            <For each={unassignedBots()}>
                              {(bot) => (
                                <option value={bot.id}>{bot.displayName} (@{bot.username})</option>
                              )}
                            </For>
                          </select>
                          <button
                            data-testid={`agent-assign-confirm-${agent.id}`}
                            type="button"
                            disabled={isAssigning()}
                            onClick={async () => {
                              // Get selected value from the sibling select
                              const sel = document.querySelector<HTMLSelectElement>(
                                `[data-testid="agent-assign-bot-select-${agent.id}"]`,
                              );
                              const botId = sel?.value;
                              if (!botId) return;
                              setIsAssigning(true);
                              setAssignError(null);
                              try {
                                await api.post(`/api/v1/admin/bots/${botId}/assign-agent`, {
                                  agentId: agent.id,
                                  configJson: '{}',
                                });
                                setAssignAgentBotId(null);
                                await loadBots();
                              } catch (err: unknown) {
                                setAssignError(getErrorMessage(err, 'Failed to assign agent.'));
                              } finally {
                                setIsAssigning(false);
                              }
                            }}
                            class={styles.assignConfirmButton}
                          >
                            {isAssigning() ? '...' : 'Confirm'}
                          </button>
                          <button
                            type="button"
                            onClick={() => setAssignAgentBotId(null)}
                            class={styles.assignCancelButton}
                          >
                            Cancel
                          </button>
                        </div>
                        <Show when={assignError()}>
                          <p class={styles.agentAssignError}>{assignError()}</p>
                        </Show>
                      </Show>
                    </Show>
                  </div>
                </div>
              )}
            </For>
          </div>
        </Show>
      </section>

      {/* ---- Create Bot Modal ---- */}
      <Show when={showCreateModal()}>
        <div
          data-testid="create-bot-modal-backdrop"
          class={styles.modalBackdrop}
          onClick={(e) => {
            if (e.target === e.currentTarget && !newToken()) handleDismissCreateModal();
          }}
        >
          <div
            data-testid="create-bot-modal"
            class={styles.modalPanel}
          >
            <div class={styles.modalHeader}>
              <h3 class={styles.modalTitle}>
                {newToken() ? 'Bot Created' : 'Create Bot'}
              </h3>
              <button
                data-testid="create-bot-modal-close"
                type="button"
                onClick={handleDismissCreateModal}
                class={styles.modalCloseButton}
              >
                &#10005;
              </button>
            </div>

            {/* Token reveal (post-create) */}
            <Show when={newToken()}>
              <div class={styles.tokenRevealSection}>
                <div class={styles.tokenWarning}>
                  This token will only be shown once. Copy it now before closing.
                </div>
                <div class={styles.tokenDisplay}>
                  {newToken()}
                </div>
                <button
                  data-testid="create-bot-copy-token"
                  type="button"
                  onClick={handleCopyToken}
                  class={styles.copyTokenButton}
                >
                  {copiedToken() ? 'Copied!' : 'Copy Token'}
                </button>
                <button
                  data-testid="create-bot-done"
                  type="button"
                  onClick={handleDismissCreateModal}
                  class={styles.doneButton}
                >
                  Done
                </button>
              </div>
            </Show>

            {/* Create form */}
            <Show when={!newToken()}>
              <form onSubmit={handleCreateBot} class={styles.modalBody}>
                <div class={styles.fieldGroup}>
                  <label for="bot-username" class={styles.fieldLabel}>
                    Username <span class={styles.requiredMark}>*</span>
                  </label>
                  <input
                    id="bot-username"
                    data-testid="create-bot-username"
                    type="text"
                    required
                    maxlength="32"
                    value={createUsername()}
                    onInput={(e) => setCreateUsername(e.currentTarget.value)}
                    class={styles.fieldInput}
                    placeholder="my-bot"
                  />
                </div>

                <div class={styles.fieldGroup}>
                  <label for="bot-display-name" class={styles.fieldLabel}>
                    Display Name <span class={styles.requiredMark}>*</span>
                  </label>
                  <input
                    id="bot-display-name"
                    data-testid="create-bot-display-name"
                    type="text"
                    required
                    maxlength="80"
                    value={createDisplayName()}
                    onInput={(e) => setCreateDisplayName(e.currentTarget.value)}
                    class={styles.fieldInput}
                    placeholder="My Bot"
                  />
                </div>

                <div class={styles.fieldGroup}>
                  <label for="bot-token-name" class={styles.fieldLabel}>
                    Token Name <span class={styles.requiredMark}>*</span>
                  </label>
                  <input
                    id="bot-token-name"
                    data-testid="create-bot-token-name"
                    type="text"
                    required
                    maxlength="80"
                    value={createTokenName()}
                    onInput={(e) => setCreateTokenName(e.currentTarget.value)}
                    class={styles.fieldInput}
                    placeholder="Bot Token"
                  />
                </div>

                <Show when={agents().length > 0}>
                  <div class={styles.fieldGroup}>
                    <label for="bot-agent" class={styles.fieldLabel}>
                      Agent (optional)
                    </label>
                    <select
                      id="bot-agent"
                      data-testid="create-bot-agent-select"
                      value={createAgentId()}
                      onChange={(e) => handleCreateAgentChange(e.currentTarget.value)}
                      class={styles.fieldInput}
                    >
                      <option value="">None</option>
                      <For each={agents()}>
                        {(agent) => (
                          <option value={agent.id}>{agent.name}</option>
                        )}
                      </For>
                    </select>
                  </div>
                </Show>

                {/* Dynamic parameter fields */}
                <Show when={selectedCreateAgent()?.manifest?.parameters && selectedCreateAgent()!.manifest!.parameters.length > 0}>
                  <div class={styles.agentParamsBox}>
                    <p class={styles.agentParamsBoxLabel}>
                      Agent Parameters
                    </p>
                    <For each={selectedCreateAgent()!.manifest!.parameters}>
                      {(param) => (
                        <ParamField
                          param={param}
                          value={createParamValues()[param.name] ?? ''}
                          onChange={(val) =>
                            setCreateParamValues((prev) => ({ ...prev, [param.name]: val }))
                          }
                        />
                      )}
                    </For>
                  </div>
                </Show>

                <Show when={createError()}>
                  <div role="alert" class={styles.formAlertError}>
                    {createError()}
                  </div>
                </Show>

                <div class={styles.modalFooter}>
                  <button
                    type="button"
                    onClick={handleDismissCreateModal}
                    class={styles.cancelButton}
                  >
                    Cancel
                  </button>
                  <button
                    data-testid="create-bot-submit"
                    type="submit"
                    disabled={isCreating()}
                    class={styles.submitButton}
                  >
                    {isCreating() ? 'Creating...' : 'Create Bot'}
                  </button>
                </div>
              </form>
            </Show>
          </div>
        </div>
      </Show>

      {/* ---- Configure Modal ---- */}
      <Show when={configuringBot()}>
        {(bot) => (
          <div
            data-testid="configure-bot-modal-backdrop"
            class={styles.modalBackdrop}
            onClick={(e) => {
              if (e.target === e.currentTarget) setConfiguringBot(null);
            }}
          >
            <div
              data-testid="configure-bot-modal"
              class={styles.modalPanel}
            >
              <div class={styles.modalHeader}>
                <h3 class={styles.modalTitle}>
                  Configure {bot().displayName}
                </h3>
                <button
                  data-testid="configure-bot-modal-close"
                  type="button"
                  onClick={() => setConfiguringBot(null)}
                  class={styles.modalCloseButton}
                >
                  &#10005;
                </button>
              </div>

              <form onSubmit={handleSaveConfig} class={styles.modalBody}>
                <Show
                  when={configuredBotAgent()?.manifest?.parameters && configuredBotAgent()!.manifest!.parameters.length > 0}
                  fallback={
                    <p class={styles.noParamsText}>
                      This agent has no configurable parameters.
                    </p>
                  }
                >
                  <For each={configuredBotAgent()!.manifest!.parameters}>
                    {(param) => (
                      <ParamField
                        param={param}
                        value={configParamValues()[param.name] ?? ''}
                        onChange={(val) =>
                          setConfigParamValues((prev) => ({ ...prev, [param.name]: val }))
                        }
                      />
                    )}
                  </For>
                </Show>

                <Show when={configError()}>
                  <div role="alert" class={styles.formAlertError}>
                    {configError()}
                  </div>
                </Show>

                <Show when={configSuccess()}>
                  <div role="status" class={styles.formAlertSuccess}>
                    {configSuccess()}
                  </div>
                </Show>

                <div class={styles.modalFooter}>
                  <button
                    type="button"
                    onClick={() => setConfiguringBot(null)}
                    class={styles.cancelButton}
                  >
                    Close
                  </button>
                  <button
                    data-testid="configure-bot-save"
                    type="submit"
                    disabled={isSavingConfig()}
                    class={styles.submitButton}
                  >
                    {isSavingConfig() ? 'Saving...' : 'Save'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </Show>
    </div>
  );
}
