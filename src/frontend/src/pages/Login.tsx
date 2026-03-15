import { createSignal, onMount } from 'solid-js';
import { useNavigate, useSearchParams, A } from '@solidjs/router';
import { useAuth } from '../stores/auth.store';
import { sanitizeRedirect } from '../utils/redirect';

export default function Login() {
  const [email, setEmail] = createSignal('');
  const [password, setPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const auth = useAuth();

  onMount(() => { document.title = 'Log In - Xcord'; });
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await auth.login({ email: email(), password: password() });
      const redirectParam = Array.isArray(searchParams.redirect) ? searchParams.redirect[0] : searchParams.redirect;
      const redirectTo = sanitizeRedirect(redirectParam);
      navigate(redirectTo);
    } catch (err: unknown) {
      setError((err as Error)?.message || 'Login failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="min-h-screen bg-xcord-bg-tertiary flex items-center justify-center">
      <form onSubmit={handleSubmit} class="bg-xcord-bg-secondary p-8 rounded-lg shadow-xl w-full max-w-md">
        <h1 data-testid="login-heading" class="text-2xl font-bold text-xcord-text-primary mb-6 text-center">Welcome back!</h1>
        {error() && <p data-testid="login-error" class="text-red-400 text-sm mb-4">{error()}</p>}
        <div class="mb-4">
          <label for="login-email" class="block text-xcord-text-secondary text-sm font-medium mb-2">Email</label>
          <input
            id="login-email"
            type="email"
            value={email()}
            onInput={(e) => setEmail(e.currentTarget.value)}
            class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 border border-xcord-border focus:border-xcord-brand focus:outline-none"
            required
          />
        </div>
        <div class="mb-6">
          <label for="login-password" class="block text-xcord-text-secondary text-sm font-medium mb-2">Password</label>
          <input
            id="login-password"
            type="password"
            value={password()}
            onInput={(e) => setPassword(e.currentTarget.value)}
            class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 border border-xcord-border focus:border-xcord-brand focus:outline-none"
            required
          />
        </div>
        <button
          data-testid="login-submit-button"
          type="submit"
          disabled={loading()}
          class="w-full bg-xcord-brand hover:bg-xcord-brand-hover text-white font-medium py-2 rounded disabled:opacity-50"
        >
          {loading() ? 'Logging in...' : 'Log In'}
        </button>
        <p class="text-xcord-text-muted text-sm mt-4 text-center">
          Need an account? <A data-testid="login-register-link" href="/register" class="text-xcord-brand hover:underline">Register</A>
        </p>
        <p class="text-xcord-text-muted text-sm mt-2 text-center">
          <A data-testid="login-forgot-password-link" href="/forgot-password" class="text-xcord-brand hover:underline">Forgot your password?</A>
        </p>
      </form>
    </div>
  );
}
