import { Show } from 'solid-js';
import styles from './ConfirmationButton.module.css';

interface ConfirmationButtonProps {
  isConfirming: boolean;
  onStartConfirm: () => void;
  onConfirm: () => void;
  onCancel: () => void;
  isLoading?: boolean;
  label?: string;
  confirmText?: string;
  testId?: string;
}

export default function ConfirmationButton(props: ConfirmationButtonProps) {
  const label = () => props.label ?? 'Delete';
  const confirmText = () => props.confirmText ?? 'Confirm?';

  return (
    <Show
      when={props.isConfirming}
      fallback={
        <button
          class={styles.deleteButton}
          data-testid={props.testId}
          onClick={props.onStartConfirm}
        >
          {label()}
        </button>
      }
    >
      <div class={styles.confirmWrapper}>
        <p class={styles.confirmPrompt}>{confirmText()}</p>
        <div class={styles.confirmActions}>
          <button
            class={styles.cancelButton}
            onClick={props.onCancel}
          >
            Cancel
          </button>
          <button
            class={styles.confirmButton}
            data-testid={props.testId ? `${props.testId}-confirm` : undefined}
            disabled={props.isLoading}
            onClick={props.onConfirm}
          >
            {props.isLoading ? `${label()}...` : 'Confirm'}
          </button>
        </div>
      </div>
    </Show>
  );
}
