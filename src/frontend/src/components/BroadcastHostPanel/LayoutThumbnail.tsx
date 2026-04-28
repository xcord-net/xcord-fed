import { Show } from 'solid-js';
import type { BroadcastLayoutPreset } from '../../stores/broadcast.store';
import styles from './LayoutThumbnail.module.css';

export default function LayoutThumbnail(props: { preset: BroadcastLayoutPreset }) {
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
