import { createSignal, onMount, Show } from 'solid-js';
import { useNavigate, A } from '@solidjs/router';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './ResetPassword.module.css';

export default function ResetPassword() {
  const [token, setToken] = createSignal('');
  const [newPassword, setNewPassword] = createSignal('');
  const [confirmPassword, setConfirmPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [success, setSuccess] = createSignal(false);
  const [loading, setLoading] = createSignal(false);
  const navigate = useNavigate();

  onMount(() => {
    document.title = 'Choose a new password - Xcord';
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
      setError('The two passwords do not match.');
      return;
    }

    if (newPassword().length < 8) {
      setError('Use at least 8 characters.');
      return;
    }

    if (!token()) {
      setError('This reset link is not valid. Request a new one.');
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
      setError(getErrorMessage(err, 'Could not save the new password. The link may have expired - request a new one.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class={styles.pageWrapper}>
      <Show
        when={!success()}
        fallback={
          <div class={styles.successCard}>
            <h1 data-testid="reset-password-success" class={styles.successHeading}>Password reset successfully!</h1>
            <p class={styles.successDescription}>
              Your password has been changed successfully. You can now log in with your new password.
            </p>
            <button
              onClick={() => navigate('/login')}
              class={styles.submitButton}
            >
              Go to Login
            </button>
          </div>
        }
      >
        <form onSubmit={handleSubmit} class={styles.card}>
          <h1 data-testid="reset-password-heading" class={styles.heading}>Choose a new password</h1>
          <p class={styles.description}>
            Enter a new password for your account. This link expires after 1 hour.
          </p>
          {error() && <p data-testid="reset-password-error" class={styles.errorText}>{error()}</p>}
          <div class={styles.fieldGroup}>
            <label for="reset-new-password" class={styles.label}>New Password</label>
            <input
              id="reset-new-password"
              type="password"
              value={newPassword()}
              onInput={(e) => setNewPassword(e.currentTarget.value)}
              class={styles.input}
              placeholder="At least 8 characters"
              required
              minLength={8}
            />
          </div>
          <div class={styles.fieldGroupLast}>
            <label for="reset-confirm-password" class={styles.label}>Confirm Password</label>
            <input
              id="reset-confirm-password"
              type="password"
              value={confirmPassword()}
              onInput={(e) => setConfirmPassword(e.currentTarget.value)}
              class={styles.input}
              placeholder="Repeat your new password"
              required
            />
          </div>
          <button
            data-testid="reset-password-submit-button"
            type="submit"
            disabled={loading() || !token()}
            class={styles.submitButton}
          >
            {loading() ? 'Saving...' : 'Save new password'}
          </button>
          <p class={styles.footerText}>
            Remembered it? <A href="/login" class={styles.link}>Back to Login</A>
          </p>
        </form>
      </Show>
    </div>
  );
}
