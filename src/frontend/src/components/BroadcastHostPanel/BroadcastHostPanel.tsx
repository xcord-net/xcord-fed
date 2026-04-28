import { Show, createEffect, createMemo, createSignal, onMount } from 'solid-js';
import {
  useBroadcast,
  type Broadcast,
  type BroadcastLayoutPreset,
} from '../../stores/broadcast.store';
import { useStreambot } from '../../stores/streambot.store';
import { useMembers } from '../../stores/member.store';
import IdlePanel from './IdlePanel';
import LivePanel from './LivePanel';
import { slotCountFor } from './constants';
import { useHostRoom } from './useHostRoom';
import styles from './BroadcastHostPanel.module.css';

interface Props {
  channelId: string;
  mode: 'idle' | 'live';
  broadcast?: Broadcast;
}

export default function BroadcastHostPanel(props: Props) {
  const broadcast = useBroadcast();
  const streambot = useStreambot();
  const members = useMembers();

  const [selectedLayout, setSelectedLayout] = createSignal<BroadcastLayoutPreset>('Grid');
  const [selectedStreambotIds, setSelectedStreambotIds] = createSignal<Set<string>>(new Set());
  const [starting, setStarting] = createSignal(false);
  const [ending, setEnding] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);

  const room = useHostRoom();

  // Load streambots on mount so the idle panel can show them.
  onMount(() => {
    streambot.load(props.channelId).then((bots) => {
      // Default-selected bots pre-checked.
      const defaults = bots.filter(b => b.isDefault).map(b => b.id);
      setSelectedStreambotIds(new Set(defaults));
    }).catch(() => {
      // Non-fatal.
    });
  });

  // Sync selectedLayout with live broadcast preset while live.
  createEffect(() => {
    if (props.mode === 'live' && props.broadcast) {
      setSelectedLayout(props.broadcast.layoutPreset);
    }
  });

  const availableStreambots = () => streambot.getForChannel(props.channelId);

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

  const slotsForLayout = createMemo(() => {
    const count = slotCountFor(selectedLayout());
    const bcast = props.broadcast;
    const slots: Array<{ slotIndex: number; userId: string | null }> = [];
    for (let i = 0; i < count; i++) {
      const occupant = bcast?.stageSlots.find(s => s.slotIndex === i);
      slots.push({ slotIndex: i, userId: occupant?.userId ?? null });
    }
    return slots;
  });

  const toggleStreambot = (id: string) => {
    setSelectedStreambotIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const handleStart = async () => {
    setError(null);
    setStarting(true);
    try {
      const res = await broadcast.startBroadcast(
        props.channelId,
        selectedLayout(),
        [...selectedStreambotIds()],
      );

      // Connect to LiveKit as publisher.
      try {
        await room.connect({ livekitUrl: res.livekitUrl, publishToken: res.publishToken });
      } catch (err) {
        console.warn('LiveKit publisher connection failed:', err);
        setError('Broadcast started but unable to connect local camera.');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start broadcast.');
    } finally {
      setStarting(false);
    }
  };

  const handleEnd = async () => {
    if (!props.broadcast) return;
    setEnding(true);
    try {
      await room.disconnect();
      await broadcast.endBroadcast(props.broadcast.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to end broadcast.');
    } finally {
      setEnding(false);
    }
  };

  const handleLayoutChange = async (preset: BroadcastLayoutPreset) => {
    setSelectedLayout(preset);
    if (props.mode === 'live' && props.broadcast) {
      try {
        await broadcast.updateLayout(props.broadcast.id, preset);
      } catch {
        // Non-fatal; event will reconcile.
      }
    }
  };

  const handleStreambotToggle = async (id: string) => {
    toggleStreambot(id);
    if (props.mode === 'live' && props.broadcast) {
      try {
        await broadcast.setActiveStreambots(props.broadcast.id, [...selectedStreambotIds()]);
      } catch {
        // Non-fatal.
      }
    }
  };

  const handleStageAssign = async (slotIndex: number, userId: string) => {
    if (!props.broadcast) return;
    try {
      await broadcast.addToStage(props.broadcast.id, userId, slotIndex);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add to stage.');
    }
  };

  const handleStageRemove = async (userId: string) => {
    if (!props.broadcast) return;
    try {
      await broadcast.removeFromStage(props.broadcast.id, userId);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to remove from stage.');
    }
  };

  return (
    <div class={styles.panel} data-testid="broadcast-host-panel" data-mode={props.mode}>
      <Show when={props.mode === 'idle'}>
        <IdlePanel
          selectedLayout={selectedLayout()}
          onSelectLayout={setSelectedLayout}
          streambots={availableStreambots()}
          selectedStreambotIds={selectedStreambotIds()}
          onToggleStreambot={toggleStreambot}
          starting={starting()}
          error={error()}
          onStart={handleStart}
        />
      </Show>

      <Show when={props.mode === 'live' && props.broadcast}>
        <LivePanel
          broadcast={props.broadcast!}
          selectedLayout={selectedLayout()}
          slots={slotsForLayout()}
          memberLookup={memberLookup()}
          streambots={availableStreambots()}
          selectedStreambotIds={selectedStreambotIds()}
          isMuted={room.isMuted()}
          isCameraOff={room.isCameraOff()}
          ending={ending()}
          error={error()}
          setPreviewRef={room.setPreviewRef}
          onLayoutChange={handleLayoutChange}
          onStreambotToggle={handleStreambotToggle}
          onStageAssign={handleStageAssign}
          onStageRemove={handleStageRemove}
          onToggleMute={room.toggleMute}
          onToggleCamera={room.toggleCamera}
          onEnd={handleEnd}
        />
      </Show>
    </div>
  );
}
