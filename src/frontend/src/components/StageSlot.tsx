import { Show, createSignal } from 'solid-js';
import styles from './StageSlot.module.css';

interface Props {
  slotIndex: number;
  userId: string | null;
  displayName?: string;
  avatarUrl?: string;
  onAssign: (userId: string) => void;
  onRemove: () => void;
}

/**
 * A single stage slot for the broadcast layout picker. Empty slots show a placeholder
 * with an inline input for manual userId assignment (MVP; a richer picker can be added
 * later). Filled slots show the user avatar/name plus a remove button.
 */
export default function StageSlot(props: Props) {
  const [editing, setEditing] = createSignal(false);
  const [inputUserId, setInputUserId] = createSignal('');

  const submit = () => {
    const id = inputUserId().trim();
    if (!id) return;
    props.onAssign(id);
    setInputUserId('');
    setEditing(false);
  };

  return (
    <div
      class={`${styles.slot} ${props.userId ? styles.slotFilled : styles.slotEmpty}`}
      data-testid={`stage-slot-${props.slotIndex}`}
    >
      <Show
        when={props.userId}
        fallback={
          <Show
            when={editing()}
            fallback={
              <button
                type="button"
                class={styles.slotPlaceholder}
                onClick={() => setEditing(true)}
                aria-label={`Assign user to slot ${props.slotIndex + 1}`}
                data-testid={`stage-slot-assign-${props.slotIndex}`}
              >
                <span class={styles.plusIcon} aria-hidden="true">+</span>
                <span class={styles.slotLabel}>Slot {props.slotIndex + 1}</span>
              </button>
            }
          >
            <div class={styles.assignForm}>
              <input
                type="text"
                placeholder="User ID"
                value={inputUserId()}
                onInput={(e) => setInputUserId(e.currentTarget.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') submit();
                  if (e.key === 'Escape') { setEditing(false); setInputUserId(''); }
                }}
                class={styles.assignInput}
                data-testid={`stage-slot-input-${props.slotIndex}`}
                autofocus
              />
              <div class={styles.assignButtons}>
                <button
                  type="button"
                  onClick={submit}
                  class={styles.confirmButton}
                  data-testid={`stage-slot-confirm-${props.slotIndex}`}
                >
                  Add
                </button>
                <button
                  type="button"
                  onClick={() => { setEditing(false); setInputUserId(''); }}
                  class={styles.cancelButton}
                >
                  Cancel
                </button>
              </div>
            </div>
          </Show>
        }
      >
        <div class={styles.slotContent}>
          <div class={styles.avatarWrap}>
            <Show
              when={props.avatarUrl}
              fallback={<span class={styles.avatarInitial}>{(props.displayName ?? props.userId ?? '?').charAt(0).toUpperCase()}</span>}
            >
              <img src={props.avatarUrl} alt="" class={styles.avatarImg} />
            </Show>
          </div>
          <span class={styles.userName}>{props.displayName ?? props.userId}</span>
          <button
            type="button"
            class={styles.removeButton}
            onClick={props.onRemove}
            aria-label={`Remove from slot ${props.slotIndex + 1}`}
            data-testid={`stage-slot-remove-${props.slotIndex}`}
          >
            &#10005;
          </button>
        </div>
      </Show>
    </div>
  );
}
