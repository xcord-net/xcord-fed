import { createSignal, onMount, Show } from 'solid-js';
import { useNavigate, useSearchParams, A } from '@solidjs/router';
import { useAuth } from '../stores/auth.store';
import { sanitizeRedirect } from '../utils/redirect';
import styles from './Login.module.css';

export default function Login() {
  const [email, setEmail] = createSignal('');
  const [password, setPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const [twoFactorToken, setTwoFactorToken] = createSignal('');
  const [twoFactorCode, setTwoFactorCode] = createSignal('');
  const [registrationEnabled, setRegistrationEnabled] = createSignal(false);
  const auth = useAuth();

  onMount(async () => {
    document.title = 'Log In - Xcord';
    try {
      const res = await fetch('/api/v1/config');
      if (res.ok) {
        const data = await res.json();
        setRegistrationEnabled(data.registrationEnabled);
      }
    } catch {}
  });
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const doNavigate = async () => {
    const redirectParam = Array.isArray(searchParams.redirect) ? searchParams.redirect[0] : searchParams.redirect;
    if (redirectParam) {
      navigate(sanitizeRedirect(redirectParam));
      return;
    }
    // Navigate to first server's channel directory
    try {
      const data = await fetch('/api/v1/users/@me/servers', { credentials: 'include' }).then(r => r.json());
      const servers = Array.isArray(data?.servers) ? data.servers : [];
      if (servers.length > 0) {
        navigate(`/channels/${servers[0].id}`, { replace: true });
        return;
      }
    } catch {}
    navigate('/');
  };

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const response = await auth.login({ email: email(), password: password() });
      if (response.requiresTwoFactor && response.twoFactorToken) {
        setTwoFactorToken(response.twoFactorToken);
      } else {
        doNavigate();
      }
    } catch (err: unknown) {
      setError((err as Error)?.message || 'Login failed');
    } finally {
      setLoading(false);
    }
  };

  const handleTwoFactorSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await auth.verifyTwoFactor(twoFactorCode(), twoFactorToken());
      doNavigate();
    } catch (err: unknown) {
      setError((err as Error)?.message || 'Invalid verification code');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class={styles.pageWrapper}>
      <Show when={!twoFactorToken()}>
        <form onSubmit={handleSubmit} class={styles.card}>
          <h1 data-testid="login-heading" class={styles.heading}>Welcome back!</h1>
          {error() && <p data-testid="login-error" class={styles.errorText}>{error()}</p>}
          <div class={styles.fieldGroup}>
            <label for="login-email" class={styles.label}>Email</label>
            <input
              id="login-email"
              data-testid="login-email-input"
              type="email"
              value={email()}
              onInput={(e) => setEmail(e.currentTarget.value)}
              class={styles.input}
              required
            />
          </div>
          <div class={styles.fieldGroupLast}>
            <label for="login-password" class={styles.label}>Password</label>
            <input
              id="login-password"
              data-testid="login-password-input"
              type="password"
              value={password()}
              onInput={(e) => setPassword(e.currentTarget.value)}
              class={styles.input}
              required
            />
          </div>
          <button
            data-testid="login-submit-button"
            type="submit"
            disabled={loading()}
            class={styles.submitButton}
          >
            {loading() ? 'Logging in...' : 'Log In'}
          </button>
          <Show when={registrationEnabled()}>
            <p class={styles.footerText}>
              Need an account? <A data-testid="login-register-link" href="/register" class={styles.link}>Register</A>
            </p>
          </Show>
          <p class={styles.footerText}>
            <A data-testid="login-forgot-password-link" href="/forgot-password" class={styles.link}>Forgot your password?</A>
          </p>
        </form>
      </Show>

      <Show when={twoFactorToken()}>
        <form data-testid="2fa-challenge-form" onSubmit={handleTwoFactorSubmit} class={styles.card}>
          <h1 data-testid="2fa-challenge-heading" class={styles.subheading}>Two-Factor Authentication</h1>
          <p class={styles.twoFactorDescription}>
            A verification code has been sent to your email address. Enter it below to complete sign-in.
          </p>
          {error() && <p data-testid="login-error" class={styles.errorText}>{error()}</p>}
          <div class={styles.fieldGroupLast}>
            <label for="2fa-login-code" class={styles.label}>Verification Code</label>
            <input
              id="2fa-login-code"
              data-testid="2fa-login-code-input"
              type="text"
              inputmode="numeric"
              maxlength={6}
              value={twoFactorCode()}
              onInput={(e) => setTwoFactorCode(e.currentTarget.value)}
              placeholder="000000"
              class={styles.codeInput}
              required
            />
          </div>
          <button
            data-testid="2fa-login-submit-button"
            type="submit"
            disabled={loading()}
            class={styles.submitButton}
          >
            {loading() ? 'Verifying...' : 'Verify'}
          </button>
          <button
            data-testid="2fa-login-back-button"
            type="button"
            class={styles.backButton}
            onClick={() => { setTwoFactorToken(''); setTwoFactorCode(''); setError(''); }}
          >
            Back to login
          </button>
        </form>
      </Show>
    </div>
  );
}
