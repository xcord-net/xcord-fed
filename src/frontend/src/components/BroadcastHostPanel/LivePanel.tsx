import { For, Show } from 'solid-js';
import type { Broadcast, BroadcastLayoutPreset } from '../../stores/broadcast.store';
import StageSlot from '../StageSlot';
import StreambotStatusBadge from '../StreambotStatusBadge';
import { LAYOUTS } from './constants';
import Flexbox from '../ui/Flexbox';
import styles from './LivePanel.module.css';
import EmptyState from '../ui/EmptyState';

interface Streambot {
  id: string;
  name: string;
  platform: string;
}

interface SlotInfo {
  slotIndex: number;
  userId: string | null;
}

interface MemberInfo {
  displayName: string;
  avatarUrl?: string;
}

interface LivePanelProps {
  broadcast: Broadcast;
  selectedLayout: BroadcastLayoutPreset;
  slots: SlotInfo[];
  memberLookup: Map<string, MemberInfo>;
  streambots: Streambot[];
  selectedStreambotIds: Set<string>;
  isMuted: boolean;
  isCameraOff: boolean;
  ending: boolean;
  error: string | null;
  setPreviewRef: (el: HTMLVideoElement | undefined) => void;
  onLayoutChange: (preset: BroadcastLayoutPreset) => void;
  onStreambotToggle: (id: string) => void;
  onStageAssign: (slotIndex: number, userId: string) => void;
  onStageRemove: (userId: string) => void;
  onToggleMute: () => void;
  onToggleCamera: () => void;
  onEnd: () => void;
}

export default function LivePanel(props: LivePanelProps) {
  const isAudioOnly = () => props.selectedLayout === 'AudioShow';

  return (
    <Flexbox direction="vertical" class={styles.liveWrap}>
      <Flexbox align="center" justify="between" class={styles.liveHeader}>
        <Flexbox align="center" gap={0.5} class={styles.liveTitleWrap}>
          <span class={styles.liveDot} aria-hidden="true" />
          <h2 class={styles.liveTitle}>Broadcasting</h2>
        </Flexbox>
        <button
          type="button"
          class={styles.endButton}
          onClick={props.onEnd}
          disabled={props.ending}
          data-testid="broadcast-end-button"
        >
          {props.ending ? 'Ending...' : 'End Broadcast'}
        </button>
      </Flexbox>

      <div class={styles.liveBody}>
        <Flexbox direction="vertical" gap={0.75} class={styles.previewCol}>
          <div class={styles.previewWrap}>
            <Show
              when={!isAudioOnly()}
              fallback={
                <div class={styles.audioPreview} data-testid="broadcast-audio-preview">
                  <div class={styles.audioWave} aria-hidden="true">
                    <i /><i /><i /><i /><i />
                  </div>
                  <p class={styles.audioPreviewText}>Voices only. No camera is going out.</p>
                </div>
              }
            >
              <video
                ref={(el) => props.setPreviewRef(el)}
                autoplay
                muted
                playsinline
                class={styles.previewVideo}
              />
            </Show>
            <Flexbox gap={0.5} class={styles.previewControls}>
              <button
                type="button"
                class={`${styles.controlButton} ${props.isMuted ? styles.controlButtonActive : ''}`}
                onClick={props.onToggleMute}
                aria-label={props.isMuted ? 'Unmute microphone' : 'Mute microphone'}
                data-testid="broadcast-mute-button"
              >
                {props.isMuted ? 'Unmute' : 'Mute'}
              </button>
              <Show when={!isAudioOnly()}>
                <button
                  type="button"
                  class={`${styles.controlButton} ${props.isCameraOff ? styles.controlButtonActive : ''}`}
                  onClick={props.onToggleCamera}
                  aria-label={props.isCameraOff ? 'Turn camera on' : 'Turn camera off'}
                  data-testid="broadcast-camera-button"
                >
                  {props.isCameraOff ? 'Camera On' : 'Camera Off'}
                </button>
              </Show>
            </Flexbox>
          </div>

          <Show when={props.broadcast.streambots.length > 0}>
            <Flexbox wrap="wrap" gap={0.375} class={styles.streambotStatuses}>
              <For each={props.broadcast.streambots}>
                {(sb) => (
                  <StreambotStatusBadge
                    name={sb.name}
                    status={sb.status}
                    error={sb.lastError}
                  />
                )}
              </For>
            </Flexbox>
          </Show>
        </Flexbox>

        <Flexbox direction="vertical" gap={1} class={styles.controlCol}>
          <Flexbox as="section" direction="vertical" gap={0.75} class={styles.section}>
            <h3 class={styles.sectionHeading}>Layout</h3>
            <p class={styles.sectionNote}>Changes appear on the stream immediately.</p>
            <Flexbox wrap="wrap" gap={0.375} class={styles.layoutRow}>
              <For each={LAYOUTS}>
                {(layout) => (
                  <button
                    type="button"
                    class={`${styles.layoutPill} ${props.selectedLayout === layout.preset ? styles.layoutPillActive : ''}`}
                    onClick={() => props.onLayoutChange(layout.preset)}
                    data-testid={`broadcast-layout-pill-${layout.preset.toLowerCase()}`}
                  >
                    {layout.label}
                  </button>
                )}
              </For>
            </Flexbox>
          </Flexbox>

          <Flexbox as="section" direction="vertical" gap={0.75} class={styles.section}>
            <h3 class={styles.sectionHeading}>Stage</h3>
            <p class={styles.sectionNote}>Add or remove people freely; the stream keeps running.</p>
            <div class={styles.stageGrid}>
              <For each={props.slots}>
                {(slot) => {
                  const info = () => slot.userId ? props.memberLookup.get(slot.userId) : undefined;
                  return (
                    <StageSlot
                      slotIndex={slot.slotIndex}
                      userId={slot.userId}
                      displayName={info()?.displayName}
                      avatarUrl={info()?.avatarUrl}
                      onAssign={(userId) => props.onStageAssign(slot.slotIndex, userId)}
                      onRemove={() => slot.userId && props.onStageRemove(slot.userId)}
                    />
                  );
                }}
              </For>
            </div>
          </Flexbox>

          <Flexbox as="section" direction="vertical" gap={0.75} class={styles.section}>
            <h3 class={styles.sectionHeading}>Destinations</h3>
            <p class={`${styles.sectionNote} ${styles.sectionNoteWarn}`} data-testid="destinations-warning">
              Changing where the broadcast goes reconnects it. Viewers on every
              destination see the stream stop and start.
            </p>
            <Show
              when={props.streambots.length > 0}
              fallback={
                <EmptyState
                  title="No restream destinations"
                  body="Add a streambot in channel settings to restream this broadcast."
                  dense
                  data-testid="live-panel-streambots-empty"
                />
              }
            >
              <Flexbox direction="vertical" gap={0.375} class={styles.streambotList}>
                <For each={props.streambots}>
                  {(bot) => (
                    <Flexbox as="label" align="center" gap={0.5} class={styles.streambotRow}>
                      <input
                        type="checkbox"
                        checked={props.selectedStreambotIds.has(bot.id)}
                        onChange={() => props.onStreambotToggle(bot.id)}
                        data-testid={`streambot-toggle-${bot.id}`}
                      />
                      <span class={styles.streambotName}>{bot.name}</span>
                      <span class={styles.streambotPlatform}>{bot.platform}</span>
                    </Flexbox>
                  )}
                </For>
              </Flexbox>
            </Show>
          </Flexbox>
        </Flexbox>
      </div>

      <Show when={props.error}>
        <div role="alert" class={styles.errorMsg}>{props.error}</div>
      </Show>
    </Flexbox>
  );
}
