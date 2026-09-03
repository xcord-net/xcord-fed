import { createSignal, onMount, Show } from 'solid-js';
import { useNavigate, useSearchParams, A } from '@solidjs/router';
import { useAuth } from '../stores/auth.store';
import { sanitizeRedirect } from '../utils/redirect';
import { api } from '../api/client';
import styles from './Login.module.css';
import { getErrorMessage } from '../utils/errors';

export default function Login() {
  const [email, setEmail] = createSignal('');
  const [password, setPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const [twoFactorToken, setTwoFactorToken] = createSignal('');
  const [twoFactorCode, setTwoFactorCode] = createSignal('');
  const [registrationEnabled, setRegistrationEnabled] = createSignal(false);
  const [devLoginEnabled, setDevLoginEnabled] = createSignal(false);
  const auth = useAuth();

  onMount(async () => {
    document.title = 'Log in - Xcord';
    try {
      const data = await api.get<{ registrationEnabled: boolean; devLoginEnabled?: boolean }>('/api/v1/config');
      setRegistrationEnabled(data.registrationEnabled);
      setDevLoginEnabled(data.devLoginEnabled === true);
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
    // Navigate to first server's channel directory, or /channels/me when the
    // user has no servers (DMs/friends view). Avoid '/' so the post-login URL
    // is always under /channels/* for any caller waiting on that pattern.
    try {
      const data = await api.get<{ servers?: Array<{ id: string }> }>('/api/v1/users/@me/servers');
      const servers = Array.isArray(data?.servers) ? data.servers : [];
      if (servers.length > 0) {
        navigate(`/channels/${servers[0].id}`, { replace: true });
        return;
      }
    } catch {}
    navigate('/channels/me', { replace: true });
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
      setError(getErrorMessage(err, 'That email and password did not match. Try again.'));
    } finally {
      setLoading(false);
    }
  };

  // Only reachable on the local dev stack: the endpoint is not mapped unless
  // TestSeed:Key is configured, and the same gate drives devLoginEnabled.
  const handleDevLogin = async () => {
    setError('');
    setLoading(true);
    try {
      await api.post('/api/v1/test/dev-login');
      await auth.validateAuth();
      doNavigate();
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Dev login failed'));
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
      setError(getErrorMessage(err, 'That code did not match. Check the current one and try again.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class={styles.pageWrapper}>
      <Show when={!twoFactorToken()}>
        <form data-testid="login-form" onSubmit={handleSubmit} class={styles.card}>
          <h1 data-testid="login-heading" class={styles.heading}>Log in to Xcord</h1>
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
            {loading() ? 'Logging in...' : 'Log in'}
          </button>
          <Show when={devLoginEnabled()}>
            <button
              data-testid="dev-login-button"
              type="button"
              disabled={loading()}
              onClick={handleDevLogin}
              class={styles.devButton}
            >
              Dev login as admin
            </button>
          </Show>
          <Show when={registrationEnabled()}>
            <p class={styles.footerText}>
              Need an account? <A data-testid="login-register-link" href="/register" class={styles.link}>Create one</A>
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
