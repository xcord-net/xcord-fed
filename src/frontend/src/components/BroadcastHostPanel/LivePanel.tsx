import { For, Show } from 'solid-js';
import type { Broadcast, BroadcastLayoutPreset } from '../../stores/broadcast.store';
import StageSlot from '../StageSlot';
import StreambotStatusBadge from '../StreambotStatusBadge';
import { LAYOUTS } from './constants';
import styles from './LivePanel.module.css';

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
  return (
    <div class={styles.liveWrap}>
      <div class={styles.liveHeader}>
        <div class={styles.liveTitleWrap}>
          <span class={styles.liveDot} aria-hidden="true" />
          <h2 class={styles.liveTitle}>Broadcasting</h2>
        </div>
        <button
          type="button"
          class={styles.endButton}
          onClick={props.onEnd}
          disabled={props.ending}
          data-testid="broadcast-end-button"
        >
          {props.ending ? 'Ending...' : 'End Broadcast'}
        </button>
      </div>

      <div class={styles.liveBody}>
        <div class={styles.previewCol}>
          <div class={styles.previewWrap}>
            <video
              ref={(el) => props.setPreviewRef(el)}
              autoplay
              muted
              playsinline
              class={styles.previewVideo}
            />
            <div class={styles.previewControls}>
              <button
                type="button"
                class={`${styles.controlButton} ${props.isMuted ? styles.controlButtonActive : ''}`}
                onClick={props.onToggleMute}
                aria-label={props.isMuted ? 'Unmute microphone' : 'Mute microphone'}
                data-testid="broadcast-mute-button"
              >
                {props.isMuted ? 'Unmute' : 'Mute'}
              </button>
              <button
                type="button"
                class={`${styles.controlButton} ${props.isCameraOff ? styles.controlButtonActive : ''}`}
                onClick={props.onToggleCamera}
                aria-label={props.isCameraOff ? 'Turn camera on' : 'Turn camera off'}
                data-testid="broadcast-camera-button"
              >
                {props.isCameraOff ? 'Camera On' : 'Camera Off'}
              </button>
            </div>
          </div>

          <Show when={props.broadcast.streambots.length > 0}>
            <div class={styles.streambotStatuses}>
              <For each={props.broadcast.streambots}>
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
                    class={`${styles.layoutPill} ${props.selectedLayout === layout.preset ? styles.layoutPillActive : ''}`}
                    onClick={() => props.onLayoutChange(layout.preset)}
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
          </section>

          <section class={styles.section}>
            <h3 class={styles.sectionHeading}>Active Streambots</h3>
            <Show
              when={props.streambots.length > 0}
              fallback={<p class={styles.emptyText}>No streambots configured.</p>}
            >
              <div class={styles.streambotList}>
                <For each={props.streambots}>
                  {(bot) => (
                    <label class={styles.streambotRow}>
                      <input
                        type="checkbox"
                        checked={props.selectedStreambotIds.has(bot.id)}
                        onChange={() => props.onStreambotToggle(bot.id)}
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

      <Show when={props.error}>
        <div role="alert" class={styles.errorMsg}>{props.error}</div>
      </Show>
    </div>
  );
}
