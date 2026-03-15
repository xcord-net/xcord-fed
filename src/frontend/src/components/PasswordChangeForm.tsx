import { createSignal } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';

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
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to change password'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="border-t border-xcord-border pt-6 mt-6">
      <h3 data-testid="change-password-heading" class="text-white font-semibold mb-4">Change Password</h3>
      <form onSubmit={handleSubmit} class="space-y-3">
        {error() && <p class="text-red-400 text-sm">{error()}</p>}
        {success() && <p data-testid="change-password-success" class="text-green-400 text-sm">{success()}</p>}
        <div>
          <label for="current-password" class="block text-xcord-text-secondary text-sm font-medium mb-1">
            Current Password
          </label>
          <input
            id="current-password"
            type="password"
            value={currentPassword()}
            onInput={(e) => setCurrentPassword(e.currentTarget.value)}
            class="w-full px-3 py-2 bg-xcord-bg-tertiary text-xcord-text-primary rounded border border-xcord-border focus:border-xcord-brand focus:outline-none"
            required
          />
        </div>
        <div>
          <label for="new-password" class="block text-xcord-text-secondary text-sm font-medium mb-1">
            New Password
          </label>
          <input
            id="new-password"
            type="password"
            value={newPassword()}
            onInput={(e) => setNewPassword(e.currentTarget.value)}
            class="w-full px-3 py-2 bg-xcord-bg-tertiary text-xcord-text-primary rounded border border-xcord-border focus:border-xcord-brand focus:outline-none"
            required
          />
        </div>
        <div>
          <label for="confirm-password" class="block text-xcord-text-secondary text-sm font-medium mb-1">
            Confirm New Password
          </label>
          <input
            id="confirm-password"
            type="password"
            value={confirmPassword()}
            onInput={(e) => setConfirmPassword(e.currentTarget.value)}
            class="w-full px-3 py-2 bg-xcord-bg-tertiary text-xcord-text-primary rounded border border-xcord-border focus:border-xcord-brand focus:outline-none"
            required
          />
        </div>
        <button
          data-testid="change-password-submit-button"
          type="submit"
          disabled={loading()}
          class="px-4 py-2 bg-xcord-brand hover:bg-xcord-brand-hover text-white rounded font-medium disabled:opacity-50"
        >
          {loading() ? 'Changing...' : 'Change Password'}
        </button>
      </form>
    </div>
  );
}
