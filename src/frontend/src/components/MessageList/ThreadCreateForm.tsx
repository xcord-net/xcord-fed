import Flexbox from '../ui/Flexbox';
import styles from './ThreadCreateForm.module.css';

interface ThreadCreateFormProps {
  value: string;
  onInput: (value: string) => void;
  onCancel: () => void;
  onSubmit: () => void;
}

/** Inline form rendered below a message for creating a thread anchored at it. */
export default function ThreadCreateForm(props: ThreadCreateFormProps) {
  return (
    <Flexbox align="center" gap={0.5} data-testid="thread-create-form" class={styles.threadCreateForm}>
      <input
        id="thread-name"
        data-testid="thread-name-input"
        type="text"
        placeholder="Thread name"
        class={styles.threadNameInput}
        value={props.value}
        onInput={(e) => props.onInput((e.target as HTMLInputElement).value)}
        onKeyDown={(e) => {
          if (e.key === 'Escape') props.onCancel();
        }}
      />
      <button
        data-testid="thread-create-submit"
        class={styles.threadCreateSubmit}
        onClick={() => props.onSubmit()}
      >
        Create Thread
      </button>
      <button
        data-testid="thread-create-cancel"
        class={styles.threadCreateCancel}
        onClick={() => props.onCancel()}
      >
        Cancel
      </button>
    </Flexbox>
  );
}
