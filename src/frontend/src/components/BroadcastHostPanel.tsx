import { For, Show, createEffect, createMemo, createSignal, onCleanup, onMount } from 'solid-js';
import { Room, RoomEvent, Track, ConnectionState } from 'livekit-client';
import {
  useBroadcast,
  type Broadcast,
  type BroadcastLayoutPreset,
} from '../stores/broadcast.store';
import { useStreambot } from '../stores/streambot.store';
import { useMembers } from '../stores/member.store';
import StageSlot from './StageSlot';
import StreambotStatusBadge from './StreambotStatusBadge';
import styles from './BroadcastHostPanel.module.css';

interface Props {
  channelId: string;
  mode: 'idle' | 'live';
  broadcast?: Broadcast;
}

const LAYOUTS: { preset: BroadcastLayoutPreset; label: string; description: string }[] = [
  { preset: 'Grid', label: 'Grid', description: 'Equal tiles for all participants' },
  { preset: 'Spotlight', label: 'Spotlight', description: 'One large, others as thumbnails' },
  { preset: 'Pip', label: 'Picture-in-Picture', description: 'Main feed with small overlay' },
  { preset: 'SideBySide', label: 'Side by Side', description: 'Two equal panels' },
];

// Number of stage slots shown for each preset.
function slotCountFor(preset: BroadcastLayoutPreset): number {
  switch (preset) {
    case 'Grid': return 8;
    case 'Spotlight': return 6;
    case 'Pip': return 2;
    case 'SideBySide': return 2;
  }
}

// LiveKit URL used by the broadcast production room. The server-issued token
// already encodes the correct room, but we need a server URL to connect. For
// MVP we reuse the same LiveKit endpoint the backend advertises via the voice
// flow; the broadcast response does not currently include it, so we read it
// from the window config when available.
function getLivekitUrl(): string {
  const w = window as unknown as { __XCORD_LIVEKIT_URL__?: string };
  return w.__XCORD_LIVEKIT_URL__ ?? '';
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
  const [isMuted, setIsMuted] = createSignal(false);
  const [isCameraOff, setIsCameraOff] = createSignal(false);

  let previewVideoRef: HTMLVideoElement | undefined;
  let liveRoom: Room | null = null;

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

  onCleanup(() => {
    if (liveRoom && liveRoom.state !== ConnectionState.Disconnected) {
      liveRoom.disconnect().catch(() => { /* non-fatal */ });
    }
    liveRoom = null;
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
      const url = getLivekitUrl();
      if (!url) {
        setError('LiveKit URL not configured. Broadcast started but local camera preview unavailable.');
        return;
      }

      try {
        liveRoom = new Room({ adaptiveStream: true, dynacast: true });
        await liveRoom.connect(url, res.publishToken);
        await liveRoom.localParticipant.setCameraEnabled(true);
        await liveRoom.localParticipant.setMicrophoneEnabled(true);
        attachLocalPreview(liveRoom);
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
      if (liveRoom && liveRoom.state !== ConnectionState.Disconnected) {
        await liveRoom.disconnect();
      }
      liveRoom = null;
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

  const toggleMute = async () => {
    if (!liveRoom) return;
    const next = !isMuted();
    try {
      await liveRoom.localParticipant.setMicrophoneEnabled(!next);
      setIsMuted(next);
    } catch (err) {
      console.warn('Failed to toggle microphone:', err);
    }
  };

  const toggleCamera = async () => {
    if (!liveRoom) return;
    const next = !isCameraOff();
    try {
      await liveRoom.localParticipant.setCameraEnabled(!next);
      setIsCameraOff(next);
    } catch (err) {
      console.warn('Failed to toggle camera:', err);
    }
  };

  const attachLocalPreview = (room: Room) => {
    if (!previewVideoRef) return;
    // Attach any already-published camera track.
    const tryAttach = () => {
      const pub = room.localParticipant
        .getTrackPublications()
        .find(p => p.source === Track.Source.Camera);
      const track = pub?.track;
      if (track && previewVideoRef) {
        track.attach(previewVideoRef);
        return true;
      }
      return false;
    };
    if (!tryAttach()) {
      room.on(RoomEvent.LocalTrackPublished, () => {
        tryAttach();
      });
    }
  };

  return (
    <div class={styles.panel} data-testid="broadcast-host-panel" data-mode={props.mode}>
      <Show when={props.mode === 'idle'}>
        <div class={styles.idleWrap}>
          <h2 class={styles.heading}>Start a Broadcast</h2>
          <p class={styles.subheading}>
            Choose a layout and restream destinations, then go live.
          </p>

          <section class={styles.section}>
            <h3 class={styles.sectionHeading}>Layout</h3>
            <div class={styles.layoutGrid}>
              <For each={LAYOUTS}>
                {(layout) => (
                  <button
                    type="button"
                    class={`${styles.layoutCard} ${selectedLayout() === layout.preset ? styles.layoutCardActive : ''}`}
                    onClick={() => setSelectedLayout(layout.preset)}
                    data-testid={`broadcast-layout-${layout.preset.toLowerCase()}`}
                  >
                    <LayoutThumbnail preset={layout.preset} />
                    <span class={styles.layoutLabel}>{layout.label}</span>
                    <span class={styles.layoutDesc}>{layout.description}</span>
                  </button>
                )}
              </For>
            </div>
          </section>

          <section class={styles.section}>
            <h3 class={styles.sectionHeading}>Restream Destinations</h3>
            <Show
              when={availableStreambots().length > 0}
              fallback={
                <p class={styles.emptyText}>
                  No streambots configured. Add some in channel settings.
                </p>
              }
            >
              <div class={styles.streambotList}>
                <For each={availableStreambots()}>
                  {(bot) => (
                    <label class={styles.streambotRow}>
                      <input
                        type="checkbox"
                        checked={selectedStreambotIds().has(bot.id)}
                        onChange={() => toggleStreambot(bot.id)}
                        data-testid={`streambot-checkbox-${bot.id}`}
                      />
                      <span class={styles.streambotName}>{bot.name}</span>
                      <span class={styles.streambotPlatform}>{bot.platform}</span>
                    </label>
                  )}
                </For>
              </div>
            </Show>
          </section>

          <Show when={error()}>
            <div role="alert" class={styles.errorMsg}>{error()}</div>
          </Show>

          <div class={styles.actions}>
            <button
              type="button"
              class={styles.startButton}
              onClick={handleStart}
              disabled={starting()}
              data-testid="broadcast-start-button"
            >
              {starting() ? 'Starting...' : 'Start Broadcast'}
            </button>
          </div>
        </div>
      </Show>

      <Show when={props.mode === 'live' && props.broadcast}>
        <div class={styles.liveWrap}>
          <div class={styles.liveHeader}>
            <div class={styles.liveTitleWrap}>
              <span class={styles.liveDot} aria-hidden="true" />
              <h2 class={styles.liveTitle}>Broadcasting</h2>
            </div>
            <button
              type="button"
              class={styles.endButton}
              onClick={handleEnd}
              disabled={ending()}
              data-testid="broadcast-end-button"
            >
              {ending() ? 'Ending...' : 'End Broadcast'}
            </button>
          </div>

          <div class={styles.liveBody}>
            <div class={styles.previewCol}>
              <div class={styles.previewWrap}>
                <video
                  ref={previewVideoRef}
                  autoplay
                  muted
                  playsinline
                  class={styles.previewVideo}
                />
                <div class={styles.previewControls}>
                  <button
                    type="button"
                    class={`${styles.controlButton} ${isMuted() ? styles.controlButtonActive : ''}`}
                    onClick={toggleMute}
                    aria-label={isMuted() ? 'Unmute microphone' : 'Mute microphone'}
                    data-testid="broadcast-mute-button"
                  >
                    {isMuted() ? 'Unmute' : 'Mute'}
                  </button>
                  <button
                    type="button"
                    class={`${styles.controlButton} ${isCameraOff() ? styles.controlButtonActive : ''}`}
                    onClick={toggleCamera}
                    aria-label={isCameraOff() ? 'Turn camera on' : 'Turn camera off'}
                    data-testid="broadcast-camera-button"
                  >
                    {isCameraOff() ? 'Camera On' : 'Camera Off'}
                  </button>
                </div>
              </div>

              <Show when={props.broadcast!.streambots.length > 0}>
                <div class={styles.streambotStatuses}>
                  <For each={props.broadcast!.streambots}>
                    {(sb) => (
                      <StreambotStatusBadge
                        name={sb.name}
                        status={sb.status}
                        error={sb.lastError}
                      />
                    )}
                  </For>
                </div>
              </Show>
            </div>

            <div class={styles.controlCol}>
              <section class={styles.section}>
                <h3 class={styles.sectionHeading}>Layout</h3>
                <div class={styles.layoutRow}>
                  <For each={LAYOUTS}>
                    {(layout) => (
                      <button
                        type="button"
                        class={`${styles.layoutPill} ${selectedLayout() === layout.preset ? styles.layoutPillActive : ''}`}
                        onClick={() => handleLayoutChange(layout.preset)}
                        data-testid={`broadcast-layout-pill-${layout.preset.toLowerCase()}`}
                      >
                        {layout.label}
                      </button>
                    )}
                  </For>
                </div>
              </section>

              <section class={styles.section}>
                <h3 class={styles.sectionHeading}>Stage</h3>
                <div class={styles.stageGrid}>
                  <For each={slotsForLayout()}>
                    {(slot) => {
                      const info = () => slot.userId ? memberLookup().get(slot.userId) : undefined;
                      return (
                        <StageSlot
                          slotIndex={slot.slotIndex}
                          userId={slot.userId}
                          displayName={info()?.displayName}
                          avatarUrl={info()?.avatarUrl}
                          onAssign={(userId) => handleStageAssign(slot.slotIndex, userId)}
                          onRemove={() => slot.userId && handleStageRemove(slot.userId)}
                        />
                      );
                    }}
                  </For>
                </div>
              </section>

              <section class={styles.section}>
                <h3 class={styles.sectionHeading}>Active Streambots</h3>
                <Show
                  when={availableStreambots().length > 0}
                  fallback={<p class={styles.emptyText}>No streambots configured.</p>}
                >
                  <div class={styles.streambotList}>
                    <For each={availableStreambots()}>
                      {(bot) => (
                        <label class={styles.streambotRow}>
                          <input
                            type="checkbox"
                            checked={selectedStreambotIds().has(bot.id)}
                            onChange={() => handleStreambotToggle(bot.id)}
                            data-testid={`streambot-toggle-${bot.id}`}
                          />
                          <span class={styles.streambotName}>{bot.name}</span>
                          <span class={styles.streambotPlatform}>{bot.platform}</span>
                        </label>
                      )}
                    </For>
                  </div>
                </Show>
              </section>
            </div>
          </div>

          <Show when={error()}>
            <div role="alert" class={styles.errorMsg}>{error()}</div>
          </Show>
        </div>
      </Show>
    </div>
  );
}

function LayoutThumbnail(props: { preset: BroadcastLayoutPreset }) {
  return (
    <div class={styles.thumbWrap} aria-hidden="true">
      <Show when={props.preset === 'Grid'}>
        <div class={styles.thumbGrid}>
          <div class={styles.thumbCell} />
          <div class={styles.thumbCell} />
          <div class={styles.thumbCell} />
          <div class={styles.thumbCell} />
        </div>
      </Show>
      <Show when={props.preset === 'Spotlight'}>
        <div class={styles.thumbSpotlight}>
          <div class={styles.thumbMain} />
          <div class={styles.thumbRowSmall}>
            <div class={styles.thumbSmall} />
            <div class={styles.thumbSmall} />
            <div class={styles.thumbSmall} />
          </div>
        </div>
      </Show>
      <Show when={props.preset === 'Pip'}>
        <div class={styles.thumbPip}>
          <div class={styles.thumbMain} />
          <div class={styles.thumbOverlay} />
        </div>
      </Show>
      <Show when={props.preset === 'SideBySide'}>
        <div class={styles.thumbSbs}>
          <div class={styles.thumbHalf} />
          <div class={styles.thumbHalf} />
        </div>
      </Show>
    </div>
  );
}
