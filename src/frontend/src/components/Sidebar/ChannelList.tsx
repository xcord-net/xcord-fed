import { For, Show } from 'solid-js';
import type { Channel } from '../../types/channel';
import ChannelItem from './ChannelItem';
import Flexbox from '../ui/Flexbox';
import styles from './ChannelList.module.css';

export interface ChannelListProps {
  isLoading: boolean;
  hasAnyChannels: boolean;
  allChannels: Channel[];
  favoriteChannels: Channel[];
  selectedChannelId: string | null;
  focusedChannelId: string | null;
  getUnreadCount: (conversationId: string) => number;
  isFavorite: (channelId: string) => boolean;
  onKeyDown: (e: KeyboardEvent) => void;
  onChannelClick: (channel: Channel) => void;
  onChannelFocus: (channelId: string) => void;
  onChannelContextMenu: (channel: Channel, x: number, y: number) => void;
}

/** The scrollable channel listbox: loading skeleton, optional favorites
 *  section, then the flat sorted list of all channels. */
export default function ChannelList(props: ChannelListProps) {
  const renderItem = (channel: Channel) => (
    <ChannelItem
      channel={channel}
      isSelected={props.selectedChannelId === channel.id}
      isFocused={props.focusedChannelId === channel.id}
      isFavorite={props.isFavorite(channel.id)}
      unreadCount={props.getUnreadCount(channel.conversationId)}
      noFocusedChannel={props.focusedChannelId === null}
      onClick={() => props.onChannelClick(channel)}
      onFocus={() => props.onChannelFocus(channel.id)}
      onContextMenu={(e) => {
        e.preventDefault();
        props.onChannelContextMenu(channel, e.clientX, e.clientY);
      }}
    />
  );

  return (
    <Flexbox
      direction="vertical"
      gap={0.125}
      class={styles.channelList}
      role="listbox"
      aria-label="Channels"
      aria-orientation="vertical"
      onKeyDown={props.onKeyDown}
    >
      {/* Loading skeleton */}
      <Show when={props.isLoading && !props.hasAnyChannels}>
        <For each={[0, 1, 2, 3, 4]}>
          {() => (
            <Flexbox align="center" justify="center" class={styles.skeletonRow} aria-hidden="true">
              <div class={styles.skeletonIcon} />
            </Flexbox>
          )}
        </For>
      </Show>

      {/* Favorite channels */}
      <Show when={props.favoriteChannels.length > 0}>
        <div data-testid="favorites-section-label" class={`expanded-only ${styles.favoritesLabel}`}>Favorites</div>
        <For each={props.favoriteChannels}>
          {(channel) => renderItem(channel)}
        </For>
        <div class={`expanded-only ${styles.sectionDivider}`} />
      </Show>

      {/* All channels (flat list, no categories) */}
      <For each={props.allChannels}>
        {(channel) => renderItem(channel)}
      </For>
    </Flexbox>
  );
}
