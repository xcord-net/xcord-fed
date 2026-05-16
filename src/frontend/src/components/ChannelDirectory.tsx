import { For, Show, createMemo } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useChannels } from '../stores/channel.store';
import { useServers } from '../stores/server.store';
import { Capability, hasCapability } from '../types/channel';
import type { Channel, Category } from '../types/channel';
import styles from './ChannelDirectory.module.css';

interface ChannelDirectoryProps {
  serverId: string;
}

function ChannelTypeIcon(props: { capabilities: number }) {
  return (
    <Show
      when={!hasCapability(props.capabilities, Capability.Voice) && !hasCapability(props.capabilities, Capability.Forum)}
      fallback={
        <Show
          when={hasCapability(props.capabilities, Capability.Voice)}
          fallback={
            /* Forum - clipboard icon */
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.channelIconSvg} aria-hidden="true">
              <path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" />
              <rect x="8" y="2" width="8" height="4" rx="1" ry="1" />
            </svg>
          }
        >
          {/* Voice - speaker icon */}
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.channelIconSvg} aria-hidden="true">
            <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" />
            <path d="M19.07 4.93a10 10 0 0 1 0 14.14" />
            <path d="M15.54 8.46a5 5 0 0 1 0 7.07" />
          </svg>
        </Show>
      }
    >
      {/* Text - hash icon */}
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.channelIconSvg} aria-hidden="true">
        <line x1="4" y1="9" x2="20" y2="9" />
        <line x1="4" y1="15" x2="20" y2="15" />
        <line x1="10" y1="3" x2="8" y2="21" />
        <line x1="16" y1="3" x2="14" y2="21" />
      </svg>
    </Show>
  );
}

export default function ChannelDirectory(props: ChannelDirectoryProps) {
  const navigate = useNavigate();
  const channelStore = useChannels();
  const serverStore = useServers();

  const server = () => serverStore.servers.find(s => s.id === props.serverId);

  const uncategorizedChannels = createMemo(() =>
    channelStore.channels
      .filter(c => !c.categoryId)
      .sort((a, b) => a.position - b.position)
  );

  const categoriesWithChannels = createMemo(() => {
    const cats = [...channelStore.categories].sort((a, b) => a.position - b.position);
    return cats.map(cat => ({
      category: cat,
      channels: channelStore.channels
        .filter(c => c.categoryId === cat.id)
        .sort((a, b) => a.position - b.position),
    })).filter(g => g.channels.length > 0);
  });

  const handleChannelClick = (channelId: string) => {
    navigate(`/channels/${props.serverId}/${channelId}`);
  };

  const ChannelCard = (cardProps: { channel: Channel }) => (
    <button
      data-testid={`channel-directory-card-${cardProps.channel.id}`}
      class={styles.channelCard}
      onClick={() => handleChannelClick(cardProps.channel.id)}
    >
      <div class={styles.channelCardHeader}>
        <span class={styles.channelIcon}>
          <ChannelTypeIcon capabilities={cardProps.channel.capabilities} />
        </span>
        <span class={styles.channelName}>{cardProps.channel.name}</span>
      </div>
      <Show when={cardProps.channel.topic}>
        <p class={styles.channelTopic}>{cardProps.channel.topic}</p>
      </Show>
    </button>
  );

  return (
    <div class={styles.container}>
      {/* Page title */}
      <div class={styles.header}>
        <h1 class={styles.serverName}>{server()?.name ?? 'Channels'}</h1>
        <p class={styles.headerSubtitle}>Select a channel to get started</p>
      </div>

      <div class={styles.content}>
        {/* Uncategorized channels */}
        <Show when={uncategorizedChannels().length > 0}>
          <div class={styles.section}>
            <div class={styles.channelGrid}>
              <For each={uncategorizedChannels()}>
                {(channel) => <ChannelCard channel={channel} />}
              </For>
            </div>
          </div>
        </Show>

        {/* Categorized channel groups */}
        <For each={categoriesWithChannels()}>
          {(group) => (
            <div class={styles.section}>
              <h2 class={styles.categoryHeading}>
                {group.category.name}
              </h2>
              <div class={styles.channelGrid}>
                <For each={group.channels}>
                  {(channel) => <ChannelCard channel={channel} />}
                </For>
              </div>
            </div>
          )}
        </For>

        {/* Empty state */}
        <Show when={channelStore.channels.length === 0 && !channelStore.isLoading}>
          <div class={styles.emptyState}>
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" class={styles.emptyIcon} aria-hidden="true">
              <line x1="4" y1="9" x2="20" y2="9" />
              <line x1="4" y1="15" x2="20" y2="15" />
              <line x1="10" y1="3" x2="8" y2="21" />
              <line x1="16" y1="3" x2="14" y2="21" />
            </svg>
            <p class={styles.emptyText}>No channels yet</p>
          </div>
        </Show>
      </div>
    </div>
  );
}
