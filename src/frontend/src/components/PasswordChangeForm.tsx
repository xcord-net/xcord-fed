import { createSignal } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './PasswordChangeForm.module.css';

// ---- Pure helpers ----

export function validatePasswordChange(
  currentPassword: string,
  newPassword: string,
  confirmPassword: string,
): string {
  if (newPassword !== confirmPassword) return 'New passwords do not match';
  if (newPassword.length < 8) return 'New password must be at least 8 characters';
  if (newPassword === currentPassword) return 'New password must be different from current password';
  return '';
}

// ---- Component ----

export default function PasswordChangeForm() {
  const [currentPassword, setCurrentPassword] = createSignal('');
  const [newPassword, setNewPassword] = createSignal('');
  const [confirmPassword, setConfirmPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [success, setSuccess] = createSignal('');
  const [loading, setLoading] = createSignal(false);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    const validationError = validatePasswordChange(currentPassword(), newPassword(), confirmPassword());
    if (validationError) {
      setError(validationError);
      return;
    }

    setLoading(true);
    try {
      await api.post('/api/v1/auth/change-password', {
        currentPassword: currentPassword(),
        newPassword: newPassword(),
      });
      setSuccess('Password changed successfully');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      // Deliberately does not close settings. It used to, a second and a half
      // later, on the reasoning that a modal you cannot see past should let you
      // out. Settings is a Deck tab now, not a modal - nothing was trapping
      // anyone - so all that timer did was yank the panel away mid-visit,
      // leaving an open Settings tab with nothing in it. Whoever wants to leave
      // can close the tab.
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to change password'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class={styles.section}>
      <h3 data-testid="change-password-heading" class={styles.heading}>Change Password</h3>
      <form onSubmit={handleSubmit} class={styles.form}>
        {error() && <p data-testid="change-password-error" class={styles.errorText}>{error()}</p>}
        {success() && <p data-testid="change-password-success" class={styles.successText}>{success()}</p>}
        <div class={styles.fieldGroup}>
          <label for="current-password" class={styles.label}>
            Current Password
          </label>
          <input
            id="current-password"
            data-testid="current-password-input"
            type="password"
            value={currentPassword()}
            onInput={(e) => setCurrentPassword(e.currentTarget.value)}
            class={styles.input}
            required
          />
        </div>
        <div class={styles.fieldGroup}>
          <label for="new-password" class={styles.label}>
            New Password
          </label>
          <input
            id="new-password"
            data-testid="new-password-input"
            type="password"
            value={newPassword()}
            onInput={(e) => setNewPassword(e.currentTarget.value)}
            class={styles.input}
            required
          />
        </div>
        <div class={styles.fieldGroup}>
          <label for="confirm-password" class={styles.label}>
            Confirm New Password
          </label>
          <input
            id="confirm-password"
            data-testid="confirm-password-input"
            type="password"
            value={confirmPassword()}
            onInput={(e) => setConfirmPassword(e.currentTarget.value)}
            class={styles.input}
            required
          />
        </div>
        <button
          data-testid="change-password-submit-button"
          type="submit"
          disabled={loading()}
          class={styles.submitButton}
        >
          {loading() ? 'Changing...' : 'Change Password'}
        </button>
      </form>
    </div>
  );
}
