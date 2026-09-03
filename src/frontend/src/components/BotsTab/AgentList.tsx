import { For, Show } from 'solid-js';
import type { Bot, BotAgent } from './types';
import Flexbox from '../ui/Flexbox';
import styles from './AgentList.module.css';
import EmptyState from '../ui/EmptyState';
import { Bot as BotGlyph } from 'lucide-solid';

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
        <Flexbox align="center" justify="center" class={styles.spinnerSmall}>
          <div class={styles.spinner} />
        </Flexbox>
      </Show>

      <Show when={!props.isLoading && props.agents.length === 0}>
        <EmptyState
          icon={BotGlyph}
          title="No agents available"
          body="Agents you install show up here, ready to add to a channel."
          data-testid="agents-empty-state"
        />
      </Show>

      <Show when={!props.isLoading && props.agents.length > 0}>
        <div class={styles.cardList}>
          <For each={props.agents}>
            {(agent) => (
              <div data-testid={`agent-card-${agent.id}`} class={styles.agentCard}>
                <Flexbox align="start" justify="between" gap={1} wrap="wrap" class={styles.agentCardInner}>
                  <div class={styles.agentInfo}>
                    <Flexbox align="center" gap={0.5} wrap="wrap" class={styles.agentNameRow}>
                      <p class={styles.agentName}>{agent.name}</p>
                      <Show when={agent.category}>
                        <span class={styles.agentCategory}>{agent.category}</span>
                      </Show>
                    </Flexbox>
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
                      <Flexbox align="center" gap={0.5} wrap="wrap" class={styles.agentAssignRow}>
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
                      </Flexbox>
                      <Show when={props.assignError}>
                        <p class={styles.agentAssignError}>{props.assignError}</p>
                      </Show>
                    </Show>
                  </Show>
                </Flexbox>
              </div>
            )}
          </For>
        </div>
      </Show>
    </section>
  );
}

export default AgentList;
