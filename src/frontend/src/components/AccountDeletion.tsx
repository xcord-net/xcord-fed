import { createSignal, Show } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import Modal from './ui/Modal';

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
    <div data-testid="danger-zone-section" class="mt-6 border-t border-red-800 pt-6">
      <h3 class="text-red-400 font-semibold text-sm uppercase tracking-wide mb-3">
        Danger Zone
      </h3>

      <Show when={error()}>
        <div class="mb-3 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">{error()}</div>
      </Show>

      <Show when={props.scheduledDeletionAt}>
        <div class="bg-red-900/20 border border-red-700 rounded p-4 mb-4">
          <p class="text-red-300 text-sm">
            Your account is scheduled for deletion on{' '}
            <strong>{formatDeletionDate(props.scheduledDeletionAt!)}</strong>.
            You can cancel this request before that date.
          </p>
        </div>
        <button
          class="bg-xcord-bg-primary border border-red-600 text-red-400 px-4 py-2 rounded hover:bg-red-900/20 transition text-sm disabled:opacity-50"
          onClick={handleCancelDeletion}
          disabled={isLoading()}
        >
          {isLoading() ? 'Cancelling...' : 'Cancel Account Deletion'}
        </button>
      </Show>

      <Show when={!props.scheduledDeletionAt}>
        <p class="text-xcord-text-muted text-sm mb-3">
          Deleting your account will remove all your data after a 14-day grace
          period. You can cancel the deletion during this time.
        </p>
        <button
          data-testid="delete-account-button"
          class="bg-red-600 text-white px-4 py-2 rounded hover:bg-red-700 transition text-sm disabled:opacity-50"
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
        <div class="p-6">
          <p class="text-xcord-text-muted text-sm mb-4">
            Enter your password to confirm. Your account will be permanently
            deleted after 14 days.
          </p>

          <Show when={error()}>
            <div class="mb-3 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">{error()}</div>
          </Show>

          <div class="mb-4">
            <label class="text-xs text-xcord-text-muted block mb-1">
              Password
            </label>
            <input
              data-testid="delete-account-password-input"
              type="password"
              class="w-full bg-xcord-bg-primary text-xcord-text-primary px-3 py-2 rounded text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
              value={password()}
              onInput={(e) => setPassword(e.currentTarget.value)}
              placeholder="Enter your password"
            />
          </div>

          <div class="flex gap-3">
            <button
              data-testid="delete-account-confirm-button"
              class="flex-1 bg-red-600 text-white py-2 rounded hover:bg-red-700 transition disabled:opacity-50"
              onClick={handleRequestDeletion}
              disabled={isLoading()}
            >
              {isLoading() ? 'Scheduling...' : 'Delete My Account'}
            </button>
            <button
              ref={cancelButtonRef}
              class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
              onClick={() => {
                setShowConfirmDialog(false);
                setPassword('');
                setError('');
              }}
            >
              Cancel
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
