import Modal from '../ui/Modal';
import Flexbox from '../ui/Flexbox';
import styles from './DeleteMessageModal.module.css';

interface DeleteMessageModalProps {
  open: boolean;
  pending?: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

/** Confirmation dialog for deleting a message. */
export default function DeleteMessageModal(props: DeleteMessageModalProps) {
  return (
    <Modal
      data-testid="delete-message-dialog"
      open={props.open}
      onClose={() => props.onClose()}
      title="Delete Message"
      size="sm"
      role="alertdialog"
    >
      <div class={styles.deleteModalBody}>
        <p class={styles.deleteModalText}>
          Are you sure you want to delete this message? This cannot be undone.
        </p>
        <Flexbox justify="end" gap={0.75} class={styles.deleteModalActions}>
          <button
            data-testid="delete-message-cancel-button"
            class={styles.cancelButton}
            onClick={() => props.onClose()}
            disabled={props.pending}
          >
            Cancel
          </button>
          <button
            data-testid="delete-message-confirm-button"
            class={styles.deleteButton}
            onClick={() => props.onConfirm()}
            disabled={props.pending}
          >
            {props.pending ? 'Deleting...' : 'Delete'}
          </button>
        </Flexbox>
      </div>
    </Modal>
  );
}
