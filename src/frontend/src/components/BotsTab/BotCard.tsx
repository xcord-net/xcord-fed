import { For, Show } from 'solid-js';
import type { Bot, BotAgent } from './types';
import { formatDate } from './helpers';
import Flexbox from '../ui/Flexbox';
import styles from './BotCard.module.css';

export interface BotCardProps {
  bot: Bot;
  agents: BotAgent[];
  actionBotId: string | null;
  assignAgentBotId: string | null;
  assignAgentId: string;
  isAssigning: boolean;
  assignError: string | null;
  onSetAssignAgentId: (id: string) => void;
  onOpenAssignAgent: (botId: string) => void;
  onCancelAssignAgent: () => void;
  onConfirmAssignAgent: (botId: string) => void;
  onConfigure: (bot: Bot) => void;
  onStart: (botId: string) => void;
  onStop: (botId: string) => void;
  onDelete: (botId: string) => void;
  onRevokeToken: (botId: string, tokenId: string) => void;
}

export function BotCard(props: BotCardProps) {
  return (
    <div data-testid={`bot-card-${props.bot.id}`} class={styles.botCard}>
      <Flexbox align="start" justify="between" gap={1} wrap="wrap" class={styles.botCardTop}>
        {/* Bot identity */}
        <Flexbox align="center" gap={0.75} class={styles.botIdentity}>
          {/* Running status dot */}
          <span
            data-testid={`bot-status-${props.bot.id}`}
            title={props.bot.isRunning ? 'Running' : 'Stopped'}
            class={props.bot.isRunning ? styles.statusDotRunning : styles.statusDotStopped}
          />
          <div class={styles.botNames}>
            <p class={styles.botDisplayName}>{props.bot.displayName}</p>
            <p class={styles.botUsername}>@{props.bot.username}</p>
            <p class={styles.botAgent}>
              Agent:{' '}
              <span class={props.bot.agentName ? styles.botAgentName : styles.botAgentUnassigned}>
                {props.bot.agentName ?? 'No agent assigned'}
              </span>
            </p>
          </div>
        </Flexbox>

        {/* Actions */}
        <Flexbox align="center" gap={0.5} wrap="wrap" class={styles.botActions}>
          {/* Start/Stop - only if agent is assigned */}
          <Show when={props.bot.agentId}>
            <Show when={props.bot.isRunning}>
              <button
                data-testid={`bot-stop-${props.bot.id}`}
                type="button"
                disabled={props.actionBotId === props.bot.id}
                onClick={() => props.onStop(props.bot.id)}
                class={styles.stopButton}
              >
                Stop
              </button>
            </Show>
            <Show when={!props.bot.isRunning}>
              <button
                data-testid={`bot-start-${props.bot.id}`}
                type="button"
                disabled={props.actionBotId === props.bot.id}
                onClick={() => props.onStart(props.bot.id)}
                class={styles.startButton}
              >
                Start
              </button>
            </Show>
          </Show>

          {/* Configure */}
          <Show when={props.bot.agentId}>
            <button
              data-testid={`bot-configure-${props.bot.id}`}
              type="button"
              onClick={() => props.onConfigure(props.bot)}
              class={styles.configureButton}
            >
              Configure
            </button>
          </Show>

          {/* Assign agent dropdown (when no agent) */}
          <Show when={!props.bot.agentId}>
            <Show when={props.assignAgentBotId === props.bot.id}>
              <Flexbox align="center" gap={0.5} class={styles.assignAgentRow}>
                <select
                  data-testid={`bot-assign-select-${props.bot.id}`}
                  value={props.assignAgentId}
                  onChange={(e) => props.onSetAssignAgentId(e.currentTarget.value)}
                  class={styles.assignSelect}
                >
                  <option value="">Select agent...</option>
                  <For each={props.agents}>
                    {(agent) => <option value={agent.id}>{agent.name}</option>}
                  </For>
                </select>
                <button
                  data-testid={`bot-assign-confirm-${props.bot.id}`}
                  type="button"
                  disabled={!props.assignAgentId || props.isAssigning}
                  onClick={() => props.onConfirmAssignAgent(props.bot.id)}
                  class={styles.assignConfirmButton}
                >
                  {props.isAssigning ? '...' : 'Assign'}
                </button>
                <button
                  type="button"
                  onClick={() => props.onCancelAssignAgent()}
                  class={styles.assignCancelButton}
                >
                  Cancel
                </button>
              </Flexbox>
              <Show when={props.assignError}>
                <p class={styles.assignError}>{props.assignError}</p>
              </Show>
            </Show>
            <Show when={props.assignAgentBotId !== props.bot.id}>
              <button
                data-testid={`bot-assign-agent-${props.bot.id}`}
                type="button"
                onClick={() => props.onOpenAssignAgent(props.bot.id)}
                class={styles.assignAgentButton}
              >
                Assign Agent
              </button>
            </Show>
          </Show>

          {/* Delete */}
          <button
            data-testid={`bot-delete-${props.bot.id}`}
            type="button"
            disabled={props.actionBotId === props.bot.id}
            onClick={() => props.onDelete(props.bot.id)}
            class={styles.deleteButton}
          >
            Delete
          </button>
        </Flexbox>
      </Flexbox>

      {/* Token list */}
      <Show when={props.bot.tokens && props.bot.tokens.length > 0}>
        <div class={styles.tokenSection}>
          <p class={styles.tokenSectionLabel}>Tokens</p>
          <For each={props.bot.tokens}>
            {(token) => (
              <Flexbox align="center" justify="between" gap={1} data-testid={`bot-token-${token.id}`} class={styles.tokenRow}>
                <div class={styles.tokenInfo}>
                  <p class={styles.tokenName}>{token.name}</p>
                  <p class={styles.tokenHash}>{token.tokenHash.slice(0, 12)}...</p>
                  <Flexbox gap={0.75} class={styles.tokenDates}>
                    <span>Created: {formatDate(token.createdAt)}</span>
                    <span>Last used: {formatDate(token.lastUsedAt)}</span>
                  </Flexbox>
                </div>
                <button
                  data-testid={`bot-revoke-token-${token.id}`}
                  type="button"
                  disabled={props.actionBotId === props.bot.id}
                  onClick={() => props.onRevokeToken(props.bot.id, token.id)}
                  class={styles.revokeButton}
                >
                  Revoke
                </button>
              </Flexbox>
            )}
          </For>
        </div>
      </Show>
    </div>
  );
}

export default BotCard;
