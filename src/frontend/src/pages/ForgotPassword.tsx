import { createSignal, Show } from 'solid-js';
import { A } from '@solidjs/router';
import { api } from '../api/client';

export default function ForgotPassword() {
  const [email, setEmail] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const [error, setError] = createSignal('');
  const [submitted, setSubmitted] = createSignal(false);

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
    <div class="min-h-screen bg-xcord-bg-tertiary flex items-center justify-center">
      <div class="bg-xcord-bg-secondary p-8 rounded-lg shadow-xl w-full max-w-md">
        <h1 class="text-2xl font-bold text-xcord-text-primary mb-2 text-center">Forgot your password?</h1>
        <p class="text-xcord-text-muted text-sm mb-6 text-center">
          Enter your email and we'll send you a reset link.
        </p>

        <Show when={submitted()}>
          <div class="text-sm text-xcord-text-primary bg-xcord-bg-tertiary border border-xcord-brand/30 rounded p-3 mb-4">
            If an account with that email exists, you'll receive a reset link shortly.
          </div>
          <p class="text-xcord-text-muted text-sm mt-4 text-center">
            <A href="/login" class="text-xcord-brand hover:underline">Back to Login</A>
          </p>
        </Show>

        <Show when={!submitted()}>
          <form onSubmit={handleSubmit}>
            {error() && <p class="text-red-400 text-sm mb-4">{error()}</p>}
            <div class="mb-6">
              <label for="forgot-email" class="block text-xcord-text-secondary text-sm font-medium mb-2">Email</label>
              <input
                id="forgot-email"
                type="email"
                value={email()}
                onInput={(e) => setEmail(e.currentTarget.value)}
                class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 border border-xcord-border focus:border-xcord-brand focus:outline-none"
                placeholder="your@email.com"
                required
              />
            </div>
            <button
              type="submit"
              disabled={loading()}
              class="w-full bg-xcord-brand hover:bg-xcord-brand-hover text-white font-medium py-2 rounded disabled:opacity-50"
            >
              {loading() ? 'Sending...' : 'Send Reset Link'}
            </button>
            <p class="text-xcord-text-muted text-sm mt-4 text-center">
              Remembered it? <A href="/login" class="text-xcord-brand hover:underline">Back to Login</A>
            </p>
          </form>
        </Show>
      </div>
    </div>
  );
}
