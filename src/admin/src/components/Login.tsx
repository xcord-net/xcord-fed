import { createSignal } from 'solid-js';
import { useAuth } from '../stores/auth.store';

export function Login() {
  const auth = useAuth();
  const [email, setEmail] = createSignal('');
  const [password, setPassword] = createSignal('');
  const [error, setError] = createSignal('');
  const [isLoading, setIsLoading] = createSignal(false);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      await auth.login({ email: email(), password: password() });
      window.location.reload();
    } catch (err: any) {
      setError(err?.message || 'Login failed');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div class="min-h-screen flex items-center justify-center bg-xcord-bg-tertiary">
      <div class="bg-xcord-bg-primary p-8 rounded-lg w-full max-w-md border border-xcord-border">
        <div class="flex flex-col items-center mb-6">
          <div class="w-12 h-12 rounded-full bg-xcord-brand flex items-center justify-center text-white font-bold text-xl mb-3">
            X
          </div>
          <h1 class="text-2xl font-bold text-white">Instance Admin</h1>
          <p class="text-sm text-xcord-text-muted mt-1">Sign in with an admin account</p>
        </div>

        <form onSubmit={handleSubmit} class="space-y-4">
          <div>
            <label class="block text-xs font-semibold uppercase text-xcord-text-secondary mb-2" for="email">
              Email
            </label>
            <input
              id="email"
              type="email"
              value={email()}
              onInput={(e) => setEmail(e.currentTarget.value)}
              required
              class="w-full px-3 py-2 bg-xcord-bg-tertiary border border-xcord-border rounded text-xcord-text-primary placeholder-xcord-text-muted focus:outline-none focus:border-xcord-brand"
              disabled={isLoading()}
              placeholder="admin@example.com"
            />
          </div>

          <div>
            <label class="block text-xs font-semibold uppercase text-xcord-text-secondary mb-2" for="password">
              Password
            </label>
            <input
              id="password"
              type="password"
              value={password()}
              onInput={(e) => setPassword(e.currentTarget.value)}
              required
              class="w-full px-3 py-2 bg-xcord-bg-tertiary border border-xcord-border rounded text-xcord-text-primary placeholder-xcord-text-muted focus:outline-none focus:border-xcord-brand"
              disabled={isLoading()}
            />
          </div>

          {error() && (
            <div class="bg-xcord-danger/10 border border-xcord-danger/30 text-xcord-danger px-4 py-3 rounded text-sm">
              {error()}
            </div>
          )}

          <button
            type="submit"
            disabled={isLoading()}
            class="w-full bg-xcord-brand text-white py-2.5 px-4 rounded font-medium hover:bg-xcord-brand-hover disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {isLoading() ? 'Signing in...' : 'Sign In'}
          </button>
        </form>
      </div>
    </div>
  );
}
