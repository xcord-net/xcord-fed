import { For, Show, createSignal, createMemo } from 'solid-js';
import { useChannels } from '../stores/channel.store';
import { api } from '../api/client';
import type { Channel, Category } from '../types/channel';

interface ChannelReorderProps {
  serverId: string;
}

interface DragState {
  type: 'channel' | 'category';
  id: string;
  categoryId?: string;
}

export default function ChannelReorder(props: ChannelReorderProps) {
  const channelStore = useChannels();
  const [dragging, setDragging] = createSignal<DragState | null>(null);
  const [dragOverId, setDragOverId] = createSignal<string | null>(null);
  const [dragOverType, setDragOverType] = createSignal<'channel' | 'category' | null>(null);

  const sortedCategories = createMemo(() =>
    [...channelStore.categories].sort((a, b) => a.position - b.position)
  );

  const uncategorizedChannels = createMemo(() =>
    channelStore.channels.filter((c) => !c.categoryId).sort((a, b) => a.position - b.position)
  );

  const channelsByCategory = (categoryId: string): Channel[] =>
    channelStore.channels.filter((c) => c.categoryId === categoryId).sort((a, b) => a.position - b.position);

  // ---- Channel drag handlers ----

  const handleChannelDragStart = (e: DragEvent, channel: Channel) => {
    setDragging({ type: 'channel', id: channel.id, categoryId: channel.categoryId });
    e.dataTransfer!.setData('text/plain', channel.id);
    e.dataTransfer!.effectAllowed = 'move';
  };

  const handleChannelDragOver = (e: DragEvent, channelId: string) => {
    e.preventDefault();
    e.dataTransfer!.dropEffect = 'move';
    setDragOverId(channelId);
    setDragOverType('channel');
  };

  const handleChannelDrop = async (e: DragEvent, targetChannel: Channel) => {
    e.preventDefault();
    const drag = dragging();
    if (!drag || drag.type !== 'channel' || drag.id === targetChannel.id) {
      resetDragState();
      return;
    }

    const sourceId = drag.id;
    const targetPosition = targetChannel.position;

    resetDragState();

    try {
      await api.put(`/api/v1/servers/${props.serverId}/channels/${sourceId}`, {
        position: targetPosition,
        categoryId: targetChannel.categoryId ?? null,
      });
      // Refresh channels after reorder
      await channelStore.fetchChannels(props.serverId);
    } catch (error) {
      console.error('Failed to reorder channel:', error);
    }
  };

  // ---- Category drag handlers ----

  const handleCategoryDragStart = (e: DragEvent, category: Category) => {
    setDragging({ type: 'category', id: category.id });
    e.dataTransfer!.setData('text/plain', category.id);
    e.dataTransfer!.effectAllowed = 'move';
  };

  const handleCategoryDragOver = (e: DragEvent, categoryId: string) => {
    e.preventDefault();
    e.dataTransfer!.dropEffect = 'move';
    setDragOverId(categoryId);
    setDragOverType('category');
  };

  const handleCategoryDrop = async (e: DragEvent, targetCategory: Category) => {
    e.preventDefault();
    const drag = dragging();
    if (!drag || drag.type !== 'category' || drag.id === targetCategory.id) {
      resetDragState();
      return;
    }

    const sourceId = drag.id;
    const targetPosition = targetCategory.position;

    resetDragState();

    try {
      await api.put(`/api/v1/servers/${props.serverId}/channels/categories/${sourceId}`, {
        position: targetPosition,
      });
      await channelStore.fetchChannels(props.serverId);
    } catch (error) {
      console.error('Failed to reorder category:', error);
    }
  };

  const handleDragEnd = () => {
    resetDragState();
  };

  const resetDragState = () => {
    setDragging(null);
    setDragOverId(null);
    setDragOverType(null);
  };

  const isDropTarget = (id: string, type: 'channel' | 'category') =>
    dragOverId() === id && dragOverType() === type && dragging()?.id !== id;

  const channelRowClass = (channel: Channel) => {
    const isTarget = isDropTarget(channel.id, 'channel');
    const isDragging = dragging()?.id === channel.id && dragging()?.type === 'channel';
    return [
      'flex items-center gap-2 px-2 py-1.5 rounded text-sm',
      isDragging ? 'opacity-40' : 'opacity-100',
      isTarget ? 'border-t-2 border-xcord-brand' : '',
    ].join(' ');
  };

  const categoryRowClass = (category: Category) => {
    const isTarget = isDropTarget(category.id, 'category');
    const isDragging = dragging()?.id === category.id && dragging()?.type === 'category';
    return [
      'flex items-center gap-2 px-1 py-1 text-xs font-semibold text-xcord-text-muted uppercase tracking-wide',
      isDragging ? 'opacity-40' : 'opacity-100',
      isTarget ? 'border-t-2 border-xcord-brand' : '',
    ].join(' ');
  };

  return (
    <div
      class="flex flex-col gap-0.5"
      onDragEnd={handleDragEnd}
    >
      {/* Uncategorized channels */}
      <For each={uncategorizedChannels()}>
        {(channel) => (
          <div
            class={channelRowClass(channel)}
            draggable={true}
            onDragStart={(e) => handleChannelDragStart(e, channel)}
            onDragOver={(e) => handleChannelDragOver(e, channel.id)}
            onDrop={(e) => handleChannelDrop(e, channel)}
            data-channel-id={channel.id}
            data-testid={`channel-row-${channel.id}`}
          >
            {/* Drag handle */}
            <span
              class="flex-shrink-0 cursor-grab text-xcord-text-muted hover:text-xcord-text-secondary select-none"
              aria-hidden="true"
              data-testid={`drag-handle-${channel.id}`}
            >
              ⠿
            </span>
            <span class="text-xcord-text-muted flex-shrink-0" aria-hidden="true">
              {channel.type === 'Voice' ? '🔊' : '#'}
            </span>
            <span class="flex-1 text-xcord-text-primary truncate">{channel.name}</span>
          </div>
        )}
      </For>

      {/* Categories with their channels */}
      <For each={sortedCategories()}>
        {(category) => (
          <div>
            {/* Category header - draggable */}
            <div
              class={categoryRowClass(category)}
              draggable={true}
              onDragStart={(e) => handleCategoryDragStart(e, category)}
              onDragOver={(e) => handleCategoryDragOver(e, category.id)}
              onDrop={(e) => handleCategoryDrop(e, category)}
              data-category-id={category.id}
              data-testid={`category-row-${category.id}`}
            >
              <span
                class="cursor-grab text-xcord-text-muted hover:text-xcord-text-secondary select-none"
                aria-hidden="true"
                data-testid={`drag-handle-cat-${category.id}`}
              >
                ⠿
              </span>
              <span>{category.name}</span>
            </div>

            {/* Channels within category */}
            <div class="pl-4 flex flex-col gap-0.5">
              <For each={channelsByCategory(category.id)}>
                {(channel) => (
                  <div
                    class={channelRowClass(channel)}
                    draggable={true}
                    onDragStart={(e) => handleChannelDragStart(e, channel)}
                    onDragOver={(e) => handleChannelDragOver(e, channel.id)}
                    onDrop={(e) => handleChannelDrop(e, channel)}
                    data-channel-id={channel.id}
                    data-testid={`channel-row-${channel.id}`}
                  >
                    <span
                      class="flex-shrink-0 cursor-grab text-xcord-text-muted hover:text-xcord-text-secondary select-none"
                      aria-hidden="true"
                      data-testid={`drag-handle-${channel.id}`}
                    >
                      ⠿
                    </span>
                    <span class="text-xcord-text-muted flex-shrink-0" aria-hidden="true">
                      {channel.type === 'Voice' ? '🔊' : '#'}
                    </span>
                    <span class="flex-1 text-xcord-text-primary truncate">{channel.name}</span>
                  </div>
                )}
              </For>
            </div>
          </div>
        )}
      </For>

      {/* Empty state */}
      <Show when={channelStore.channels.length === 0 && channelStore.categories.length === 0}>
        <p class="text-xcord-text-muted text-sm px-2 py-4 text-center">No channels to reorder.</p>
      </Show>
    </div>
  );
}
