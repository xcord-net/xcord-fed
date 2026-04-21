import { For, Show, createMemo } from 'solid-js';
import { useBroadcast, type Broadcast } from '../stores/broadcast.store';
import { useMembers } from '../stores/member.store';
import styles from './BroadcastGreenRoom.module.css';

interface Props {
  broadcast: Broadcast;
}

/**
 * Host-side green room listing. Shows the current stage slots with remove actions.
 * Adding to stage is handled via the host panel's StageSlot inputs (MVP).
 */
export default function BroadcastGreenRoom(props: Props) {
  const broadcast = useBroadcast();
  const members = useMembers();

  const memberLookup = createMemo(() => {
    const map = new Map<string, { displayName: string; avatarUrl?: string }>();
    for (const m of members.members) {
      map.set(m.userId, {
        displayName: m.nickname || m.displayName || m.username,
        avatarUrl: m.avatarUrl,
      });
    }
    return map;
  });

  const onStage = () => props.broadcast.stageSlots;

  const handleRemove = async (userId: string) => {
    try {
      await broadcast.removeFromStage(props.broadcast.id, userId);
    } catch {
      // Non-fatal; status will refresh via signalR event.
    }
  };

  return (
    <div class={styles.panel} data-testid="broadcast-green-room">
      <div class={styles.header}>
        <h3 class={styles.title}>Green Room</h3>
        <span class={styles.subtitle}>
          {onStage().length} on stage
        </span>
      </div>
      <Show
        when={onStage().length > 0}
        fallback={<p class={styles.emptyText}>No one is on stage yet.</p>}
      >
        <ul class={styles.list}>
          <For each={onStage()}>
            {(slot) => {
              const info = () => memberLookup().get(slot.userId);
              return (
                <li class={styles.listItem} data-testid={`green-room-slot-${slot.slotIndex}`}>
                  <div class={styles.slotBadge}>#{slot.slotIndex + 1}</div>
                  <div class={styles.avatarWrap}>
                    <Show
                      when={info()?.avatarUrl}
                      fallback={
                        <span class={styles.avatarInitial}>
                          {(info()?.displayName ?? slot.userId).charAt(0).toUpperCase()}
                        </span>
                      }
                    >
                      <img src={info()!.avatarUrl} alt="" class={styles.avatarImg} />
                    </Show>
                  </div>
                  <span class={styles.userName}>
                    {info()?.displayName ?? slot.userId}
                  </span>
                  <button
                    type="button"
                    class={styles.removeButton}
                    onClick={() => handleRemove(slot.userId)}
                    aria-label="Remove from stage"
                    data-testid={`green-room-remove-${slot.userId}`}
                  >
                    Remove
                  </button>
                </li>
              );
            }}
          </For>
        </ul>
      </Show>
    </div>
  );
}
