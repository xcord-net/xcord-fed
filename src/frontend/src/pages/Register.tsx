import { createSignal, onMount, Show } from 'solid-js';
import { useNavigate, useSearchParams, A } from '@solidjs/router';
import { useAuth } from '../stores/auth.store';
import { api } from '../api/client';
import Captcha from '../components/Captcha';
import styles from './Register.module.css';
import { getErrorMessage } from '../utils/errors';

export default function Register() {
  const [username, setUsername] = createSignal('');
  const [email, setEmail] = createSignal('');
  const [password, setPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const [captchaId, setCaptchaId] = createSignal('');
  const [captchaAnswer, setCaptchaAnswer] = createSignal('');
  const auth = useAuth();

  const navigate = useNavigate();
  const [searchParams] = useSearchParams<{ invite?: string }>();
  const inviteCode = () => searchParams.invite ?? '';

  onMount(async () => {
    document.title = 'Create your account - Xcord';
    try {
      const data = await api.get<{ registrationEnabled: boolean }>('/api/v1/config');
      // An invite is its own authorisation: the instance may have public
      // registration off and still want invited people to be able to sign up.
      if (!data.registrationEnabled && !inviteCode()) {
        navigate('/login');
        return;
      }
    } catch {}
  });

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await auth.register({
        username: username(),
        displayName: username(),
        email: email(),
        password: password(),
        captchaId: captchaId(),
        captchaAnswer: captchaAnswer(),
        inviteCode: inviteCode() || undefined,
      });
      // Confirm the address first - joining a server needs the email_confirmed
      // claim - then carry the invite through so the last thing they do is land
      // in the server they were invited to.
      navigate(
        inviteCode()
          ? `/confirm-email?redirect=${encodeURIComponent(`/invite/${inviteCode()}`)}`
          : '/confirm-email',
      );
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Could not create the account. Check the details above and try again.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class={styles.pageWrapper}>
      <form data-testid="register-form" onSubmit={handleSubmit} class={styles.card}>
        <h1 data-testid="register-heading" class={styles.heading}>Create an account</h1>
        <Show when={inviteCode()}>
          <p data-testid="register-invite-notice" class={styles.footerText}>
            You have been invited. Create an account to join.
          </p>
        </Show>
        {error() && <p data-testid="register-error" class={styles.errorText}>{error()}</p>}
        <div class={styles.fieldGroup}>
          <label for="reg-username" class={styles.label}>Username</label>
          <input
            id="reg-username"
            data-testid="register-username-input"
            type="text"
            value={username()}
            onInput={(e) => setUsername(e.currentTarget.value)}
            class={styles.input}
            required
          />
        </div>
        <div class={styles.fieldGroup}>
          <label for="reg-email" class={styles.label}>Email</label>
          <input
            id="reg-email"
            data-testid="register-email-input"
            type="email"
            value={email()}
            onInput={(e) => setEmail(e.currentTarget.value)}
            class={styles.input}
            required
          />
        </div>
        <div class={styles.fieldGroupLast}>
          <label for="reg-password" class={styles.label}>Password</label>
          <input
            id="reg-password"
            data-testid="register-password-input"
            type="password"
            value={password()}
            onInput={(e) => setPassword(e.currentTarget.value)}
            class={styles.input}
            required
            autocomplete="new-password"
          />
        </div>
        <div class={styles.fieldGroupLast}>
          <Captcha onSolved={(id, answer) => { setCaptchaId(id); setCaptchaAnswer(answer); }} />
        </div>
        <button
          data-testid="register-submit-button"
          type="submit"
          disabled={loading() || (captchaAnswer() === '' && captchaId() !== 'disabled')}
          class={styles.submitButton}
        >
          {loading() ? 'Creating account...' : 'Create account'}
        </button>
        <p class={styles.footerText}>
          Already have an account?{' '}
          <A
            data-testid="register-login-link"
            href={inviteCode()
              ? `/login?redirect=${encodeURIComponent(`/invite/${inviteCode()}`)}`
              : '/login'}
            class={styles.link}
          >
            Log in
          </A>
        </p>
      </form>
    </div>
  );
}
