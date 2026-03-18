import { createSignal, onMount } from 'solid-js';
import { useNavigate, A } from '@solidjs/router';
import { useAuth } from '../stores/auth.store';

export default function Register() {
  const [username, setUsername] = createSignal('');
  const [email, setEmail] = createSignal('');
  const [password, setPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const auth = useAuth();

  onMount(() => { document.title = 'Register - Xcord'; });
  const navigate = useNavigate();

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await auth.register({ username: username(), displayName: username(), email: email(), password: password() });
      navigate('/confirm-email');
    } catch (err: unknown) {
      setError((err as Error)?.message || 'Registration failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="min-h-screen bg-xcord-bg-tertiary flex items-center justify-center">
      <form onSubmit={handleSubmit} class="bg-xcord-bg-secondary p-8 rounded-lg shadow-xl w-full max-w-md">
        <h1 data-testid="register-heading" class="text-2xl font-bold text-xcord-text-primary mb-6 text-center">Create an account</h1>
        {error() && <p data-testid="register-error" class="text-red-400 text-sm mb-4">{error()}</p>}
        <div class="mb-4">
          <label for="reg-username" class="block text-xcord-text-secondary text-sm font-medium mb-2">Username</label>
          <input
            id="reg-username"
            type="text"
            value={username()}
            onInput={(e) => setUsername(e.currentTarget.value)}
            class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 border border-xcord-border focus:border-xcord-brand focus:outline-none"
            required
          />
        </div>
        <div class="mb-4">
          <label for="reg-email" class="block text-xcord-text-secondary text-sm font-medium mb-2">Email</label>
          <input
            id="reg-email"
            type="email"
            value={email()}
            onInput={(e) => setEmail(e.currentTarget.value)}
            class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 border border-xcord-border focus:border-xcord-brand focus:outline-none"
            required
          />
        </div>
        <div class="mb-6">
          <label for="reg-password" class="block text-xcord-text-secondary text-sm font-medium mb-2">Password</label>
          <input
            id="reg-password"
            type="password"
            value={password()}
            onInput={(e) => setPassword(e.currentTarget.value)}
            class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 border border-xcord-border focus:border-xcord-brand focus:outline-none"
            required
            autocomplete="new-password"
          />
        </div>
        <button
          data-testid="register-submit-button"
          type="submit"
          disabled={loading()}
          class="w-full bg-xcord-brand hover:bg-xcord-brand-hover text-white font-medium py-2 rounded disabled:opacity-50"
        >
          {loading() ? 'Creating account...' : 'Register'}
        </button>
        <p class="text-xcord-text-muted text-sm mt-4 text-center">
          Already have an account? <A href="/login" class="text-xcord-brand hover:underline">Log in</A>
        </p>
      </form>
    </div>
  );
}
