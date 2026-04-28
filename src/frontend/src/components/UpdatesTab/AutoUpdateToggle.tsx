import { Show } from 'solid-js';
import styles from './UpdatesTab.module.css';

interface AutoUpdateToggleProps {
  enabled: boolean;
  hubConnected: boolean;
  isToggling: boolean;
  toggleError: string | null;
  onToggle: () => void;
}

export default function AutoUpdateToggle(props: AutoUpdateToggleProps) {
  return (
    <>
      <div
        data-testid="updates-auto-toggle-section"
        class={styles.autoToggleSection}
      >
        <div>
          <p class={styles.autoToggleLabel}>Automatic Updates</p>
          <p class={styles.autoToggleDescription}>
            Automatically apply updates when new versions are published by the hub.
          </p>
        </div>
        <button
          data-testid="updates-auto-toggle-button"
          type="button"
          disabled={props.isToggling || !props.hubConnected}
          onClick={props.onToggle}
          class={`${styles.toggleButton} ${props.enabled ? styles.toggleButtonOn : styles.toggleButtonOff}`}
          role="switch"
          aria-checked={props.enabled}
          aria-label="Toggle automatic updates"
        >
          <span
            class={`${styles.toggleThumb} ${props.enabled ? styles.toggleThumbOn : styles.toggleThumbOff}`}
          />
        </button>
      </div>

      <Show when={props.toggleError}>
        <div
          data-testid="updates-toggle-error"
          role="alert"
          class={styles.toggleError}
        >
          {props.toggleError}
        </div>
      </Show>
    </>
  );
}
