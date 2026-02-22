import { createSignal } from 'solid-js';
import { useNavigate, A } from '@solidjs/router';
import { useAuth } from '../stores/auth.store';

export default function Login() {
  const [email, setEmail] = createSignal('');
  const [password, setPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const auth = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await auth.login({ email: email(), password: password() });
      navigate('/channels/me');
    } catch (err: unknown) {
      setError((err as Error)?.message || 'Login failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="min-h-screen bg-xcord-bg-tertiary flex items-center justify-center">
      <form onSubmit={handleSubmit} class="bg-xcord-bg-secondary p-8 rounded-lg shadow-xl w-full max-w-md">
        <h1 class="text-2xl font-bold text-xcord-text-primary mb-6 text-center">Welcome back!</h1>
        {error() && <p class="text-red-400 text-sm mb-4">{error()}</p>}
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
          type="submit"
          disabled={loading()}
          class="w-full bg-xcord-brand hover:bg-xcord-brand-hover text-white font-medium py-2 rounded disabled:opacity-50"
        >
          {loading() ? 'Logging in...' : 'Log In'}
        </button>
        <p class="text-xcord-text-muted text-sm mt-4 text-center">
          Need an account? <A href="/register" class="text-xcord-brand hover:underline">Register</A>
        </p>
        <p class="text-xcord-text-muted text-sm mt-2 text-center">
          <A href="/forgot-password" class="text-xcord-brand hover:underline">Forgot your password?</A>
        </p>
      </form>
    </div>
  );
}
