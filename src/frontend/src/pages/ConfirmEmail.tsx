import { createSignal, onMount, Show } from 'solid-js';
import { useNavigate, useSearchParams } from '@solidjs/router';
import { api } from '../api/client';
import { sanitizeRedirect } from '../utils/redirect';
import styles from './ConfirmEmail.module.css';
import { getErrorMessage } from '../utils/errors';

export default function ConfirmEmail() {
  const [code, setCode] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const [resent, setResent] = createSignal(false);
  const [resending, setResending] = createSignal(false);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams<{ redirect?: string }>();

  onMount(() => { document.title = 'Confirm your email - Xcord'; });

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await api.post(`/api/v1/auth/confirm-email?code=${encodeURIComponent(code())}`);
      // Refresh the token so it includes the email_confirmed claim (cookie updated server-side)
      await api.post('/api/v1/auth/refresh');
      // Someone who arrived from an invite goes back to it - joining a server
      // needs the email_confirmed claim they only just earned, so the invite
      // has to wait for this step rather than be forgotten by it.
      navigate(sanitizeRedirect(searchParams.redirect));
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'That code did not match. Check the email and try again.'));
    } finally {
      setLoading(false);
    }
  };

  // The resend endpoint has always existed; nothing in the UI reached it, so a
  // lost or expired code was a dead end.
  const handleResend = async () => {
    setError('');
    setResent(false);
    setResending(true);
    try {
      await api.post('/api/v1/auth/resend-confirmation');
      setResent(true);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Could not send another code. Try again in a moment.'));
    } finally {
      setResending(false);
    }
  };

  return (
    <div class={styles.pageWrapper}>
      <form data-testid="confirm-email-form" onSubmit={handleSubmit} class={styles.card}>
        <h1 data-testid="confirm-email-heading" class={styles.heading}>Confirm your email</h1>
        <p class={styles.description}>
          We sent a 6-digit code to your email address. Enter it below to verify your account.
        </p>
        {error() && <p data-testid="confirm-email-error" class={styles.errorText}>{error()}</p>}
        <div class={styles.fieldGroup}>
          <label for="confirmation-code" class={styles.label}>Confirmation Code</label>
          <input
            id="confirmation-code"
            data-testid="confirmation-code-input"
            type="text"
            inputMode="numeric"
            maxLength={6}
            value={code()}
            onInput={(e) => setCode(e.currentTarget.value)}
            class={styles.codeInput}
            placeholder="000000"
            required
          />
        </div>
        <button
          data-testid="confirm-email-submit-button"
          type="submit"
          disabled={loading() || code().length !== 6}
          class={styles.submitButton}
        >
          {loading() ? 'Confirming...' : 'Confirm'}
        </button>
        <Show when={resent()}>
          <p data-testid="confirm-email-resent" class={styles.description}>
            A new code is on its way.
          </p>
        </Show>
        <button
          data-testid="confirm-email-resend-button"
          type="button"
          disabled={resending()}
          onClick={handleResend}
          class={styles.submitButton}
        >
          {resending() ? 'Sending...' : 'Resend code'}
        </button>
      </form>
    </div>
  );
}
