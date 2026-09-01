import { Show } from 'solid-js';
import MarkdownRenderer from '../MarkdownRenderer';
import type { Message } from '../../types/message';
import MessageActionBar from './MessageActionBar';
import ThreadCreateForm from './ThreadCreateForm';
import MessageContent from './MessageContent';
import { formatTime } from './helpers';
import type { LaneAssignment } from './lanes';
import ReplyReference from './ReplyReference';
import Flexbox from '../ui/Flexbox';
import styles from './MessageRow.module.css';

interface MessageRowProps {
  message: Message;
  conversationId: string;
  serverId?: string;
  channelId?: string;
  grouped: boolean;
  /** Set when this message belongs to a reply-connected exchange. */
  lane?: LaneAssignment;
  /** 0-1 conversation heat for this row's lane. */
  laneHeat?: number;
  onJumpTo?: (messageId: string) => void;
  isAuthor: boolean;
  canDelete: boolean;
  isReactionPickerOpen: boolean;
  isThreadCreateOpen: boolean;
  threadNameValue: string;
  onReply: () => void;
  onEdit: () => void;
  onDelete: () => void;
  onToggleReactionPicker: () => void;
  onCloseReactionPicker: () => void;
  onSelectReaction: (emoji: string) => void;
  onTogglePin: () => void;
  onStartThread: () => void;
  onThreadNameInput: (value: string) => void;
  onThreadCreateSubmit: () => void;
  onThreadCreateCancel: () => void;
}

/** A single message row including the hover action bar, optional thread-create form,
 *  and either a grouped (no header) or full (avatar + header) body. */
export default function MessageRow(props: MessageRowProps) {
  return (
    <div
      data-message-id={props.message.id}
      data-testid={`message-row-${props.message.id}`}
      data-lane-id={props.lane?.laneId}
      classList={{
        [styles.messageRow]: true,
        [styles.messageRowGrouped]: props.grouped,
        [styles.messageRowLaned]: !!props.lane,
      }}
      style={props.lane
        ? {
            '--lane-color': `var(--color-xcord-lane-${props.lane.colorIndex + 1})`,
            '--lane-heat': String(props.laneHeat ?? 0),
          }
        : undefined}
    >
      {/* Lane rail. Absolutely positioned into the scroll container's padding so a
          lane forming or dissolving never reflows the message column. */}
      <Show when={props.lane}>
        {(lane) => (
          <span
            aria-hidden="true"
            data-testid="lane-rail"
            classList={{
              [styles.laneRail]: true,
              [styles.laneRailContinuesUp]: lane().continuesPrevious,
              [styles.laneRailContinuesDown]: lane().continuesNext,
            }}
          />
        )}
      </Show>

      <MessageActionBar
        message={props.message}
        isReactionPickerOpen={props.isReactionPickerOpen}
        canEdit={props.isAuthor}
        canDelete={props.canDelete}
        showThreadButton={!!props.channelId}
        serverId={props.serverId}
        onReply={props.onReply}
        onEdit={props.onEdit}
        onDelete={props.onDelete}
        onToggleReactionPicker={props.onToggleReactionPicker}
        onCloseReactionPicker={props.onCloseReactionPicker}
        onSelectReaction={props.onSelectReaction}
        onTogglePin={props.onTogglePin}
        onStartThread={props.onStartThread}
      />

      {/* Thread creation form - shown inline below the action bar when triggered */}
      <Show when={props.isThreadCreateOpen}>
        <ThreadCreateForm
          value={props.threadNameValue}
          onInput={props.onThreadNameInput}
          onCancel={props.onThreadCreateCancel}
          onSubmit={props.onThreadCreateSubmit}
        />
      </Show>

      <Show
        when={!props.grouped}
        fallback={
          <div class={styles.groupedContent}>
            <span class={styles.groupedText} data-testid="message-content">
              <MarkdownRenderer content={props.message.content} />
            </span>
            <MessageContent
              message={props.message}
              conversationId={props.conversationId}
              serverId={props.serverId}
              isAuthor={props.isAuthor}
            />
          </div>
        }
      >
        <Flexbox gap={0.75} class={styles.messageWithHeader}>
          {/* Avatar */}
          <div class={styles.avatar}>
            <Show
              when={props.message.authorAvatarUrl}
              fallback={props.message.authorUsername?.charAt(0).toUpperCase() || 'U'}
            >
              <img
                src={props.message.authorAvatarUrl}
                alt={props.message.authorUsername}
                class={styles.avatarImg}
              />
            </Show>
          </div>

          {/* Message content */}
          <div class={styles.messageBody}>
            <Flexbox align="baseline" gap={0.5} class={styles.messageHeader}>
              {/* Card 170: group color applied inline to username */}
              <span
                class={styles.authorName}
                style={{ color: props.message.authorGroupColor ?? 'white' }}
              >
                {props.message.authorUsername || 'Unknown User'}
              </span>
              <span class={styles.messageTimestamp}>{formatTime(props.message.createdAt)}</span>
            </Flexbox>

            <Show when={props.message.replyTo}>
              {(replyTo) => (
                <ReplyReference
                  replyTo={replyTo()}
                  onJumpTo={() => props.onJumpTo?.(replyTo().id)}
                />
              )}
            </Show>

            <div class={styles.messageContent} data-testid="message-content">
              <MarkdownRenderer content={props.message.content} />
            </div>
            <MessageContent
              message={props.message}
              conversationId={props.conversationId}
              serverId={props.serverId}
              isAuthor={props.isAuthor}
            />
          </div>
        </Flexbox>
      </Show>
    </div>
  );
}
