import { For } from 'solid-js';
import { useToasts } from '../../stores/toast.store';
import styles from './Toaster.module.css';

/**
 * App-wide toast host. Mounted once near the root. Errors are announced
 * assertively to screen readers; success/info politely. Without this, async
 * failures (failed reaction, upload, pin) were swallowed with only a console
 * error, leaving users to think the action had succeeded.
 */
export default function Toaster() {
  const toasts = useToasts();

  return (
    <div class={styles.region} aria-live="polite" aria-atomic="false">
      <For each={toasts.toasts}>
        {(toast) => (
          <div
            class={styles.toast}
            classList={{
              [styles.error]: toast.kind === 'error',
              [styles.success]: toast.kind === 'success',
              [styles.info]: toast.kind === 'info',
            }}
            role={toast.kind === 'error' ? 'alert' : 'status'}
            aria-live={toast.kind === 'error' ? 'assertive' : 'polite'}
            data-testid={`toast-${toast.kind}`}
          >
            <span class={styles.message}>{toast.message}</span>
            <button
              class={styles.dismiss}
              aria-label="Dismiss notification"
              onClick={() => toasts.dismiss(toast.id)}
            >
              &times;
            </button>
          </div>
        )}
      </For>
    </div>
  );
}
