import Modal from '../ui/Modal';
import styles from './EndPollConfirmModal.module.css';

interface EndPollConfirmModalProps {
  open: boolean;
  isEndingPoll: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

export default function EndPollConfirmModal(props: EndPollConfirmModalProps) {
  return (
    <Modal
      open={props.open}
      onClose={props.onCancel}
      title="End Poll"
      size="sm"
      role="alertdialog"
    >
      <div class={styles.confirmModalBody}>
        <p class={styles.confirmModalText}>End this poll? Voting will be disabled and no further votes can be cast.</p>
        <div class={styles.confirmModalActions}>
          <button
            class={styles.confirmCancelButton}
            onClick={props.onCancel}
          >
            Cancel
          </button>
          <button
            class={styles.confirmEndButton}
            disabled={props.isEndingPoll}
            onClick={props.onConfirm}
          >
            End Poll
          </button>
        </div>
      </div>
    </Modal>
  );
}
