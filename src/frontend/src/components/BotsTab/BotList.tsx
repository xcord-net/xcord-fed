import { For, Show } from 'solid-js';
import type { Bot, BotAgent } from './types';
import { BotCard } from './BotCard';
import Flexbox from '../ui/Flexbox';
import styles from './BotList.module.css';
import EmptyState from '../ui/EmptyState';
import { Bot as BotGlyph } from 'lucide-solid';

export interface BotListProps {
  bots: Bot[];
  agents: BotAgent[];
  isLoading: boolean;
  actionBotId: string | null;
  assignAgentBotId: string | null;
  assignAgentId: string;
  isAssigning: boolean;
  assignError: string | null;
  onCreate: () => void;
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

export function BotList(props: BotListProps) {
  return (
    <section>
      <Flexbox align="center" justify="between" class={styles.sectionHeader}>
        <h3 class={styles.sectionTitle}>Your Bots</h3>
        <button
          data-testid="bots-create-button"
          type="button"
          onClick={() => props.onCreate()}
          class={styles.createBotButton}
        >
          + Create Bot
        </button>
      </Flexbox>

      <Show when={props.isLoading}>
        <Flexbox align="center" justify="center" class={styles.spinnerContainer}>
          <div class={styles.spinner} />
        </Flexbox>
      </Show>

      <Show when={!props.isLoading && props.bots.length === 0}>
        <EmptyState
          icon={BotGlyph}
          title="No bots yet"
          body="A bot gets its own token and can post, moderate, and answer commands."
          data-testid="bots-empty-state"
        />
      </Show>

      <Show when={!props.isLoading && props.bots.length > 0}>
        <div class={styles.cardList}>
          <For each={props.bots}>
            {(bot) => (
              <BotCard
                bot={bot}
                agents={props.agents}
                actionBotId={props.actionBotId}
                assignAgentBotId={props.assignAgentBotId}
                assignAgentId={props.assignAgentId}
                isAssigning={props.isAssigning}
                assignError={props.assignError}
                onSetAssignAgentId={props.onSetAssignAgentId}
                onOpenAssignAgent={props.onOpenAssignAgent}
                onCancelAssignAgent={props.onCancelAssignAgent}
                onConfirmAssignAgent={props.onConfirmAssignAgent}
                onConfigure={props.onConfigure}
                onStart={props.onStart}
                onStop={props.onStop}
                onDelete={props.onDelete}
                onRevokeToken={props.onRevokeToken}
              />
            )}
          </For>
        </div>
      </Show>
    </section>
  );
}

export default BotList;
