import { Show } from 'solid-js';

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
          class="bg-red-500 text-white px-3 py-1 rounded text-sm hover:bg-red-600 flex-shrink-0"
          data-testid={props.testId}
          onClick={props.onStartConfirm}
        >
          {label()}
        </button>
      }
    >
      <div class="flex flex-col items-end space-y-1 flex-shrink-0">
        <p class="text-xs text-xcord-text-muted">{confirmText()}</p>
        <div class="flex space-x-2">
          <button
            class="bg-xcord-bg-primary text-xcord-text-muted px-2 py-1 rounded text-xs hover:bg-xcord-bg-tertiary"
            onClick={props.onCancel}
          >
            Cancel
          </button>
          <button
            class="bg-red-600 text-white px-2 py-1 rounded text-xs hover:bg-red-700 disabled:opacity-50"
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
