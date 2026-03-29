import { createSignal, onMount, Show } from 'solid-js';
import { A } from '@solidjs/router';
import { api } from '../api/client';
import styles from './ForgotPassword.module.css';

export default function ForgotPassword() {
  const [email, setEmail] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const [error, setError] = createSignal('');
  const [submitted, setSubmitted] = createSignal(false);

  onMount(() => { document.title = 'Forgot Password - Xcord'; });

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      // Always returns 204 - no information leakage about whether the email exists
      await api.post('/api/v1/auth/forgot-password', { email: email() });
      setSubmitted(true);
    } catch {
      // Show a generic error only for network failures; the endpoint itself
      // always returns 204 so this path means the server was unreachable.
      setError('Network error. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class={styles.pageWrapper}>
      <div class={styles.card}>
        <h1 data-testid="forgot-password-heading" class={styles.heading}>Forgot your password?</h1>
        <p class={styles.description}>
          Enter your email and we'll send you a reset link.
        </p>

        <Show when={submitted()}>
          <div data-testid="forgot-password-success" class={styles.successBox}>
            If an account with that email exists, you'll receive a reset link shortly.
          </div>
          <p class={styles.footerText}>
            <A href="/login" class={styles.link}>Back to Login</A>
          </p>
        </Show>

        <Show when={!submitted()}>
          <form onSubmit={handleSubmit}>
            {error() && <p data-testid="forgot-password-error" class={styles.errorText}>{error()}</p>}
            <div class={styles.fieldGroup}>
              <label for="forgot-email" class={styles.label}>Email</label>
              <input
                id="forgot-email"
                type="email"
                value={email()}
                onInput={(e) => setEmail(e.currentTarget.value)}
                class={styles.input}
                placeholder="your@email.com"
                required
              />
            </div>
            <button
              data-testid="forgot-password-submit-button"
              type="submit"
              disabled={loading()}
              class={styles.submitButton}
            >
              {loading() ? 'Sending...' : 'Send Reset Link'}
            </button>
            <p class={styles.footerText}>
              Remembered it? <A href="/login" class={styles.link}>Back to Login</A>
            </p>
          </form>
        </Show>
      </div>
    </div>
  );
}
