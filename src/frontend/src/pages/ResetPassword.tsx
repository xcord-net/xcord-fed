import { createSignal, onMount, Show } from 'solid-js';
import { useNavigate, A } from '@solidjs/router';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';

export default function ResetPassword() {
  const [token, setToken] = createSignal('');
  const [newPassword, setNewPassword] = createSignal('');
  const [confirmPassword, setConfirmPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [success, setSuccess] = createSignal(false);
  const [loading, setLoading] = createSignal(false);
  const navigate = useNavigate();

  onMount(() => {
    document.title = 'Reset Password - Xcord';
    const params = new URLSearchParams(window.location.search);
    const t = params.get('token');
    if (t) {
      setToken(t);
    }
  });

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');

    if (newPassword() !== confirmPassword()) {
      setError('Passwords do not match');
      return;
    }

    if (newPassword().length < 8) {
      setError('Password must be at least 8 characters');
      return;
    }

    if (!token()) {
      setError('Invalid or missing reset token');
      return;
    }

    setLoading(true);
    try {
      await api.post('/api/v1/auth/reset-password', {
        token: token(),
        newPassword: newPassword(),
      });
      setSuccess(true);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to reset password. The link may have expired.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="min-h-screen bg-xcord-bg-tertiary flex items-center justify-center">
      <Show
        when={!success()}
        fallback={
          <div class="bg-xcord-bg-secondary p-8 rounded-lg shadow-xl w-full max-w-md text-center">
            <h1 data-testid="reset-password-success" class="text-2xl font-bold text-xcord-text-primary mb-4">Password reset successfully!</h1>
            <p class="text-xcord-text-muted text-sm mb-6">
              Your password has been changed successfully. You can now log in with your new password.
            </p>
            <button
              onClick={() => navigate('/login')}
              class="w-full bg-xcord-brand hover:bg-xcord-brand-hover text-white font-medium py-2 rounded"
            >
              Go to Login
            </button>
          </div>
        }
      >
        <form onSubmit={handleSubmit} class="bg-xcord-bg-secondary p-8 rounded-lg shadow-xl w-full max-w-md">
          <h1 data-testid="reset-password-heading" class="text-2xl font-bold text-xcord-text-primary mb-2 text-center">Choose a new password</h1>
          <p class="text-xcord-text-muted text-sm mb-6 text-center">
            Enter a new password for your account. This link expires after 1 hour.
          </p>
          {error() && <p data-testid="reset-password-error" class="text-red-400 text-sm mb-4">{error()}</p>}
          <div class="mb-4">
            <label for="reset-new-password" class="block text-xcord-text-secondary text-sm font-medium mb-2">New Password</label>
            <input
              id="reset-new-password"
              type="password"
              value={newPassword()}
              onInput={(e) => setNewPassword(e.currentTarget.value)}
              class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 border border-xcord-border focus:border-xcord-brand focus:outline-none"
              placeholder="At least 8 characters"
              required
              minLength={8}
            />
          </div>
          <div class="mb-6">
            <label for="reset-confirm-password" class="block text-xcord-text-secondary text-sm font-medium mb-2">Confirm Password</label>
            <input
              id="reset-confirm-password"
              type="password"
              value={confirmPassword()}
              onInput={(e) => setConfirmPassword(e.currentTarget.value)}
              class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 border border-xcord-border focus:border-xcord-brand focus:outline-none"
              placeholder="Repeat your new password"
              required
            />
          </div>
          <button
            data-testid="reset-password-submit-button"
            type="submit"
            disabled={loading() || !token()}
            class="w-full bg-xcord-brand hover:bg-xcord-brand-hover text-white font-medium py-2 rounded disabled:opacity-50"
          >
            {loading() ? 'Resetting...' : 'Reset Password'}
          </button>
          <p class="text-xcord-text-muted text-sm mt-4 text-center">
            Remembered it? <A href="/login" class="text-xcord-brand hover:underline">Back to Login</A>
          </p>
        </form>
      </Show>
    </div>
  );
}
