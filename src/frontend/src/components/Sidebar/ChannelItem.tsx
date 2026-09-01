import { Show } from 'solid-js';
import { Capability, hasCapability } from '../../types/channel';
import type { Channel } from '../../types/channel';
import { ChannelIcon } from './icons';
import styles from './ChannelItem.module.css';

export interface ChannelItemProps {
  channel: Channel;
  isSelected: boolean;
  isFocused: boolean;
  isFavorite: boolean;
  unreadCount: number;
  /** True when no channel is currently focused; the selected channel takes the
   *  initial tab stop so keyboard users land on it. */
  noFocusedChannel: boolean;
  onClick: () => void;
  onFocus: () => void;
  onContextMenu: (e: MouseEvent & { currentTarget: HTMLButtonElement }) => void;
}

function channelTypeLabel(capabilities: number): string {
  if (hasCapability(capabilities, Capability.Voice)) return 'Voice channel';
  if (hasCapability(capabilities, Capability.Streaming)) return 'Streaming channel';
  if (hasCapability(capabilities, Capability.Forum)) return 'Forum channel';
  return 'Text channel';
}

function buttonStateClass(isSelected: boolean, hasUnread: boolean): string {
  if (isSelected) return styles.channelButtonActive;
  if (hasUnread) return styles.channelButtonUnread;
  return styles.channelButtonDefault;
}

/** A single channel row including the type icon, name, unread badge, and
 *  favorite-aware data attributes. */
export default function ChannelItem(props: ChannelItemProps) {
  const hasUnread = () => props.unreadCount > 0;
  const stateClass = () => buttonStateClass(props.isSelected, hasUnread());
  const tabIndex = () =>
    props.isFocused || (props.noFocusedChannel && props.isSelected) ? 0 : -1;
  const ariaLabel = () =>
    `${channelTypeLabel(props.channel.capabilities)} ${props.channel.name}${hasUnread() ? `, ${props.unreadCount} unread` : ''}`;

  return (
    <button
      role="option"
      aria-selected={props.isSelected}
      aria-label={ariaLabel()}
      data-channel-id={props.channel.id}
      data-channel-name={props.channel.name}
      data-testid={`channel-item-${props.channel.id}`}
      data-favorited={props.isFavorite ? 'true' : 'false'}
      tabindex={tabIndex()}
      class={`${styles.channelButton} channel-icon-wrap ${stateClass()}`}
      classList={{ favorited: props.isFavorite }}
      onClick={() => props.onClick()}
      onFocus={() => props.onFocus()}
      onContextMenu={(e) => props.onContextMenu(e)}
    >
      {/* Channel type icon with unread indicator */}
      <span class={styles.channelIconWrap}>
        <ChannelIcon capabilities={props.channel.capabilities} class={styles.channelTypeIcon} />
        <Show when={hasUnread()}>
          <span class={`${styles.unreadDot} collapsed-only`} aria-hidden="true" />
        </Show>
      </span>

      {/* Channel name + unread badge - visible only when expanded (CSS) */}
      <span class={`expanded-only ${styles.channelNameArea} ${hasUnread() ? styles.channelNameAreaUnread : ''}`}>
        <span
          class={styles.unreadIndicatorDot}
          style={{ visibility: hasUnread() ? 'visible' : 'hidden' }}
          aria-hidden="true"
        />
        <span class={styles.channelName}>{props.channel.name}</span>
        <Show when={hasUnread()}>
          <span
            data-testid="channel-unread-badge"
            class={styles.unreadBadge}
            aria-label={`${props.unreadCount} unread messages`}
          >
            {props.unreadCount > 99 ? '99+' : props.unreadCount}
          </span>
        </Show>
      </span>
    </button>
  );
}
