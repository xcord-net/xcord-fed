import { For, Show } from 'solid-js';
import type { BroadcastLayoutPreset } from '../../stores/broadcast.store';
import LayoutThumbnail from './LayoutThumbnail';
import { LAYOUTS } from './constants';
import styles from './IdlePanel.module.css';

interface Streambot {
  id: string;
  name: string;
  platform: string;
}

interface IdlePanelProps {
  selectedLayout: BroadcastLayoutPreset;
  onSelectLayout: (preset: BroadcastLayoutPreset) => void;
  streambots: Streambot[];
  selectedStreambotIds: Set<string>;
  onToggleStreambot: (id: string) => void;
  starting: boolean;
  error: string | null;
  onStart: () => void;
}

export default function IdlePanel(props: IdlePanelProps) {
  return (
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
                class={`${styles.layoutCard} ${props.selectedLayout === layout.preset ? styles.layoutCardActive : ''}`}
                onClick={() => props.onSelectLayout(layout.preset)}
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
          when={props.streambots.length > 0}
          fallback={
            <p class={styles.emptyText}>
              No streambots configured. Add some in channel settings.
            </p>
          }
        >
          <div class={styles.streambotList}>
            <For each={props.streambots}>
              {(bot) => (
                <label class={styles.streambotRow}>
                  <input
                    type="checkbox"
                    checked={props.selectedStreambotIds.has(bot.id)}
                    onChange={() => props.onToggleStreambot(bot.id)}
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

      <Show when={props.error}>
        <div role="alert" class={styles.errorMsg}>{props.error}</div>
      </Show>

      <div class={styles.actions}>
        <button
          type="button"
          class={styles.startButton}
          onClick={props.onStart}
          disabled={props.starting}
          data-testid="broadcast-start-button"
        >
          {props.starting ? 'Starting...' : 'Start Broadcast'}
        </button>
      </div>
    </div>
  );
}
