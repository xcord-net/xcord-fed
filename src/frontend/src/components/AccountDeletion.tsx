import { createSignal, Show } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import Modal from './ui/Modal';
import Flexbox from './ui/Flexbox';
import styles from './AccountDeletion.module.css';

interface AccountDeletionProps {
  scheduledDeletionAt?: string | null;
  onDeletionScheduled?: (date: string) => void;
  onDeletionCancelled?: () => void;
}

// ---- Pure helpers ----

export function validateDeletionRequest(password: string): string {
  if (!password.trim()) return 'Password is required';
  return '';
}

export function formatDeletionDate(isoDate: string): string {
  return new Date(isoDate).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

// ---- Component ----

export default function AccountDeletion(props: AccountDeletionProps) {
  const [showConfirmDialog, setShowConfirmDialog] = createSignal(false);
  const [password, setPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [isLoading, setIsLoading] = createSignal(false);
  let cancelButtonRef!: HTMLButtonElement;

  const handleRequestDeletion = async () => {
    const validationError = validateDeletionRequest(password());
    if (validationError) {
      setError(validationError);
      return;
    }

    setIsLoading(true);
    setError('');

    try {
      const response = await api.post<{ scheduledDeletionAt: string }>(
        '/api/v1/users/@me/delete',
        { password: password() }
      );
      setShowConfirmDialog(false);
      setPassword('');
      props.onDeletionScheduled?.(response.scheduledDeletionAt);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to schedule account deletion'));
    } finally {
      setIsLoading(false);
    }
  };

  const handleCancelDeletion = async () => {
    setIsLoading(true);
    setError('');

    try {
      await api.post('/api/v1/users/@me/cancel-deletion');
      props.onDeletionCancelled?.();
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to cancel account deletion'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div data-testid="danger-zone-section" class={styles.section}>
      <h3 class={styles.dangerHeading}>
        Danger Zone
      </h3>

      <Show when={error()}>
        <div class={styles.errorBanner}>{error()}</div>
      </Show>

      <Show when={props.scheduledDeletionAt}>
        <div data-testid="scheduled-deletion-warning" class={styles.scheduledWarning}>
          <p class={styles.scheduledWarningText}>
            Your account is scheduled for deletion on{' '}
            <strong>{formatDeletionDate(props.scheduledDeletionAt!)}</strong>.
            You can cancel this request before that date.
          </p>
        </div>
        <button
          data-testid="cancel-deletion-button"
          class={styles.cancelButton}
          onClick={handleCancelDeletion}
          disabled={isLoading()}
        >
          {isLoading() ? 'Cancelling...' : 'Cancel Account Deletion'}
        </button>
      </Show>

      <Show when={!props.scheduledDeletionAt}>
        <p class={styles.warningText}>
          Deleting your account will remove all your data after a 14-day grace
          period. You can cancel the deletion during this time.
        </p>
        <button
          data-testid="delete-account-button"
          class={styles.deleteButton}
          onClick={() => {
            setError('');
            setShowConfirmDialog(true);
          }}
          disabled={isLoading()}
        >
          Delete Account
        </button>
      </Show>

      <Modal
        data-testid="delete-account-dialog"
        open={showConfirmDialog()}
        onClose={() => { setShowConfirmDialog(false); setPassword(''); setError(''); }}
        title="Confirm Account Deletion"
        size="md"
        role="alertdialog"
        initialFocusRef={cancelButtonRef}
      >
        <div class={styles.modalBody}>
          <p class={styles.modalDescription}>
            Enter your password to confirm. Your account will be permanently
            deleted after 14 days.
          </p>

          <Show when={error()}>
            <div class={styles.errorBanner}>{error()}</div>
          </Show>

          <div class={styles.fieldGroup}>
            <label class={styles.fieldLabel}>
              Password
            </label>
            <input
              data-testid="delete-account-password-input"
              type="password"
              class={styles.passwordInput}
              value={password()}
              onInput={(e) => setPassword(e.currentTarget.value)}
              placeholder="Enter your password"
            />
          </div>

          <Flexbox gap={0.75}>
            <button
              data-testid="delete-account-confirm-button"
              class={styles.confirmDeleteButton}
              onClick={handleRequestDeletion}
              disabled={isLoading()}
            >
              {isLoading() ? 'Scheduling...' : 'Delete My Account'}
            </button>
            <button
              data-testid="delete-account-cancel-button"
              ref={cancelButtonRef}
              class={styles.modalCancelButton}
              onClick={() => {
                setShowConfirmDialog(false);
                setPassword('');
                setError('');
              }}
            >
              Cancel
            </button>
          </Flexbox>
        </div>
      </Modal>
    </div>
  );
}
