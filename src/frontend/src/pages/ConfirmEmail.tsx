import { createSignal, onMount } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { api } from '../api/client';
import styles from './ConfirmEmail.module.css';

export default function ConfirmEmail() {
  const [code, setCode] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const navigate = useNavigate();

  onMount(() => { document.title = 'Confirm Email - Xcord'; });

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await api.post(`/api/v1/auth/confirm-email?code=${encodeURIComponent(code())}`);
      // Refresh the token so it includes the email_confirmed claim (cookie updated server-side)
      await api.post('/api/v1/auth/refresh');
      navigate('/channels/me');
    } catch (err: unknown) {
      setError((err as Error)?.message || 'Invalid confirmation code');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class={styles.pageWrapper}>
      <form onSubmit={handleSubmit} class={styles.card}>
        <h1 data-testid="confirm-email-heading" class={styles.heading}>Confirm your email</h1>
        <p class={styles.description}>
          We sent a 6-digit code to your email address. Enter it below to verify your account.
        </p>
        {error() && <p class={styles.errorText}>{error()}</p>}
        <div class={styles.fieldGroup}>
          <label for="confirmation-code" class={styles.label}>Confirmation Code</label>
          <input
            id="confirmation-code"
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
      </form>
    </div>
  );
}
