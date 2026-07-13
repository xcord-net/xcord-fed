import { createSignal, onMount } from 'solid-js';
import { useNavigate, A } from '@solidjs/router';
import { useAuth } from '../stores/auth.store';
import { api } from '../api/client';
import Captcha from '../components/Captcha';
import styles from './Register.module.css';

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

  onMount(async () => {
    document.title = 'Register - Xcord';
    try {
      const data = await api.get<{ registrationEnabled: boolean }>('/api/v1/config');
      if (!data.registrationEnabled) {
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
      });
      navigate('/confirm-email');
    } catch (err: unknown) {
      setError((err as Error)?.message || 'Registration failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class={styles.pageWrapper}>
      <form data-testid="register-form" onSubmit={handleSubmit} class={styles.card}>
        <h1 data-testid="register-heading" class={styles.heading}>Create an account</h1>
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
          {loading() ? 'Creating account...' : 'Register'}
        </button>
        <p class={styles.footerText}>
          Already have an account? <A href="/login" class={styles.link}>Log in</A>
        </p>
      </form>
    </div>
  );
}
