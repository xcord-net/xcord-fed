import { For, Show } from 'solid-js';
import type { BroadcastLayoutPreset } from '../../stores/broadcast.store';
import LayoutThumbnail from './LayoutThumbnail';
import { LAYOUTS } from './constants';
import Flexbox from '../ui/Flexbox';
import styles from './IdlePanel.module.css';
import EmptyState from '../ui/EmptyState';

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
    <Flexbox direction="vertical" gap={1.5} class={styles.idleWrap}>
      <h2 class={styles.heading}>Start a Broadcast</h2>
      <p class={styles.subheading}>
        Choose a layout and restream destinations, then go live.
      </p>

      <Flexbox as="section" direction="vertical" gap={0.75} class={styles.section}>
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
      </Flexbox>

      <Flexbox as="section" direction="vertical" gap={0.75} class={styles.section}>
        <h3 class={styles.sectionHeading}>Restream Destinations</h3>
        <Show
          when={props.streambots.length > 0}
          fallback={
            <EmptyState
              title="No restream destinations"
              body="Add a streambot in channel settings to send this broadcast somewhere else too."
              dense
              data-testid="idle-panel-streambots-empty"
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
                    onChange={() => props.onToggleStreambot(bot.id)}
                    data-testid={`streambot-checkbox-${bot.id}`}
                  />
                  <span class={styles.streambotName}>{bot.name}</span>
                  <span class={styles.streambotPlatform}>{bot.platform}</span>
                </Flexbox>
              )}
            </For>
          </Flexbox>
        </Show>
      </Flexbox>

      <Show when={props.error}>
        <div role="alert" class={styles.errorMsg}>{props.error}</div>
      </Show>

      <Flexbox justify="end" class={styles.actions}>
        <button
          type="button"
          class={styles.startButton}
          onClick={props.onStart}
          disabled={props.starting}
          data-testid="broadcast-start-button"
        >
          {props.starting ? 'Starting...' : 'Start Broadcast'}
        </button>
      </Flexbox>
    </Flexbox>
  );
}
