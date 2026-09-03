import styles from './WaveformMark.module.css';

export interface WaveformMarkProps {
  /**
   * Animate the bars. Per the design spec the mark moves only on real audio
   * activity, so this is driven by live voice state, never by decoration.
   */
  live?: boolean;
  class?: string;
}

/** The `)))(((`  brand mark, reduced to five bars for the Home tab glyph. */
export default function WaveformMark(props: WaveformMarkProps) {
  return (
    <span
      class={`${styles.wave} ${props.live ? styles.live : ''} ${props.class ?? ''}`}
      data-testid="deck-waveform-mark"
      data-live={String(!!props.live)}
      aria-hidden="true"
    >
      <i /><i /><i /><i /><i />
    </span>
  );
}
