import { For, Show, createMemo } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useChannels } from '../stores/channel.store';
import { useServers } from '../stores/server.store';
import type { Channel, Category } from '../types/channel';
import { ChannelIcon } from './Sidebar/icons';
import styles from './ChannelDirectory.module.css';
import EmptyState from './ui/EmptyState';
import { Hash } from 'lucide-solid';

interface ChannelDirectoryProps {
  serverId: string;
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
      // The directory is the channel list now that the Deck has replaced the
      // sidebar, so it carries the attribute the list has always carried.
      // Anything that knows a channel by name rather than by id - every caller
      // that has just created one - has nothing else to find it by.
      data-channel-name={cardProps.channel.name}
      class={styles.channelCard}
      onClick={() => handleChannelClick(cardProps.channel.id)}
    >
      <div class={styles.channelCardHeader}>
        <span class={styles.channelIcon}>
          <ChannelIcon capabilities={cardProps.channel.capabilities} class={styles.channelIconSvg} />
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
          <EmptyState
            icon={Hash}
            title="No channels yet"
            body="Channels created in this community appear here."
            data-testid="channel-directory-empty"
          />
        </Show>
      </div>
    </div>
  );
}
