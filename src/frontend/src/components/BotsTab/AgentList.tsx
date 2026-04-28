import { For, Show } from 'solid-js';
import type { Bot, BotAgent } from './types';
import styles from './AgentList.module.css';

export interface AgentListProps {
  agents: BotAgent[];
  isLoading: boolean;
  unassignedBots: Bot[];
  assignAgentBotId: string | null;
  isAssigning: boolean;
  assignError: string | null;
  /** Open the inline "assign to bot" form for the given agent. */
  onOpenAssignToBot: (agentId: string) => void;
  /** Set the assign-row open state (used by the bot select to close on empty). */
  onSetAssignAgentBotIdRaw: (value: string | null) => void;
  onCancelAssignToBot: () => void;
  /** Confirm assignment - implementation reads the bot id from the DOM by data-testid. */
  onConfirmAssignToBot: (agentId: string) => Promise<void> | void;
}

export function AgentList(props: AgentListProps) {
  return (
    <section>
      <h3 class={styles.sectionTitle} style="margin-bottom:1rem">
        Available Agents
      </h3>

      <Show when={props.isLoading}>
        <div class={styles.spinnerSmall}>
          <div class={styles.spinner} />
        </div>
      </Show>

      <Show when={!props.isLoading && props.agents.length === 0}>
        <div data-testid="agents-empty-state" class={styles.emptyState}>
          No agents available.
        </div>
      </Show>

      <Show when={!props.isLoading && props.agents.length > 0}>
        <div class={styles.cardList}>
          <For each={props.agents}>
            {(agent) => (
              <div data-testid={`agent-card-${agent.id}`} class={styles.agentCard}>
                <div class={styles.agentCardInner}>
                  <div class={styles.agentInfo}>
                    <div class={styles.agentNameRow}>
                      <p class={styles.agentName}>{agent.name}</p>
                      <Show when={agent.category}>
                        <span class={styles.agentCategory}>{agent.category}</span>
                      </Show>
                    </div>
                    <Show when={agent.description}>
                      <p class={styles.agentDescription}>{agent.description}</p>
                    </Show>
                    <Show
                      when={
                        agent.manifest?.parameters &&
                        agent.manifest.parameters.length > 0
                      }
                    >
                      <p class={styles.agentParams}>
                        Parameters:{' '}
                        {agent.manifest!.parameters.map((p) => p.name).join(', ')}
                      </p>
                    </Show>
                  </div>

                  {/* Assign to Bot button */}
                  <Show when={props.unassignedBots.length > 0}>
                    <Show when={props.assignAgentBotId !== agent.id + '-agent'}>
                      <button
                        data-testid={`agent-assign-${agent.id}`}
                        type="button"
                        onClick={() => props.onOpenAssignToBot(agent.id)}
                        class={styles.assignToBotButton}
                      >
                        Assign to Bot
                      </button>
                    </Show>
                    <Show when={props.assignAgentBotId === agent.id + '-agent'}>
                      <div class={styles.agentAssignRow}>
                        <select
                          data-testid={`agent-assign-bot-select-${agent.id}`}
                          onChange={(e) =>
                            props.onSetAssignAgentBotIdRaw(
                              e.currentTarget.value ? agent.id + '-agent' : null,
                            )
                          }
                          class={styles.assignSelect}
                        >
                          <option value="">Select bot...</option>
                          <For each={props.unassignedBots}>
                            {(bot) => (
                              <option value={bot.id}>
                                {bot.displayName} (@{bot.username})
                              </option>
                            )}
                          </For>
                        </select>
                        <button
                          data-testid={`agent-assign-confirm-${agent.id}`}
                          type="button"
                          disabled={props.isAssigning}
                          onClick={() => props.onConfirmAssignToBot(agent.id)}
                          class={styles.assignConfirmButton}
                        >
                          {props.isAssigning ? '...' : 'Confirm'}
                        </button>
                        <button
                          type="button"
                          onClick={() => props.onCancelAssignToBot()}
                          class={styles.assignCancelButton}
                        >
                          Cancel
                        </button>
                      </div>
                      <Show when={props.assignError}>
                        <p class={styles.agentAssignError}>{props.assignError}</p>
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
  );
}

export default AgentList;
