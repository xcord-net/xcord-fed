import Modal from '../ui/Modal';
import styles from './DeleteGroupModal.module.css';

interface DeleteGroupModalProps {
  open: boolean;
  groupName: string | undefined;
  isDeleting: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

export default function DeleteGroupModal(props: DeleteGroupModalProps) {
  return (
    <Modal
      open={props.open}
      onClose={props.onClose}
      title="Delete Group"
      size="sm"
      role="alertdialog"
    >
      <div class={styles.dialogBody}>
        <p class={styles.dialogText}>
          Are you sure you want to delete the group "{props.groupName}"? Members with this group will lose its roles.
        </p>
        <div class={styles.dialogActions}>
          <button
            class={styles.dialogCancelButton}
            onClick={props.onClose}
          >
            Cancel
          </button>
          <button
            class={styles.dialogDeleteButton}
            disabled={props.isDeleting}
            onClick={props.onConfirm}
          >
            {props.isDeleting ? 'Deleting...' : 'Delete'}
          </button>
        </div>
      </div>
    </Modal>
  );
}
