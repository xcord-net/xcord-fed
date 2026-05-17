import Modal from '../ui/Modal';
import Flexbox from '../ui/Flexbox';
import sharedStyles from './Sidebar.module.css';
import styles from './LeaveServerModal.module.css';

export interface LeaveServerModalProps {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

/** Confirm-leave dialog. Same data-testids as the original inline modal. */
export default function LeaveServerModal(props: LeaveServerModalProps) {
  return (
    <Modal
      data-testid="leave-server-dialog"
      open={props.open}
      onClose={() => props.onClose()}
      title="Leave Server"
      size="sm"
      role="alertdialog"
    >
      <div class={styles.leaveModalBody}>
        <p class={styles.leaveModalText}>Are you sure you want to leave this server?</p>
        <Flexbox justify="end" gap={0.75}>
          <button
            data-testid="leave-server-cancel-button"
            type="button"
            onClick={() => props.onClose()}
            class={sharedStyles.cancelButton}
          >
            Cancel
          </button>
          <button
            data-testid="leave-server-confirm-button"
            type="button"
            onClick={() => props.onConfirm()}
            class={styles.leaveButton}
          >
            Leave Server
          </button>
        </Flexbox>
      </div>
    </Modal>
  );
}
