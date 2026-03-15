import { createSignal, onMount } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { api } from '../api/client';

export default function ConfirmEmail() {
  const [code, setCode] = createSignal('');
  const [error, setError] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const navigate = useNavigate();

  onMount(() => { document.title = 'Confirm Email - Xcord'; });

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await api.post(`/api/v1/auth/confirm-email?code=${encodeURIComponent(code())}`);
      // Refresh the token so it includes the email_confirmed claim (cookie updated server-side)
      await api.post('/api/v1/auth/refresh');
      navigate('/channels/me');
    } catch (err: unknown) {
      setError((err as Error)?.message || 'Invalid confirmation code');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="min-h-screen bg-xcord-bg-tertiary flex items-center justify-center">
      <form onSubmit={handleSubmit} class="bg-xcord-bg-secondary p-8 rounded-lg shadow-xl w-full max-w-md">
        <h1 data-testid="confirm-email-heading" class="text-2xl font-bold text-xcord-text-primary mb-2 text-center">Confirm your email</h1>
        <p class="text-xcord-text-muted text-sm mb-6 text-center">
          We sent a 6-digit code to your email address. Enter it below to verify your account.
        </p>
        {error() && <p class="text-red-400 text-sm mb-4">{error()}</p>}
        <div class="mb-6">
          <label for="confirmation-code" class="block text-xcord-text-secondary text-sm font-medium mb-2">Confirmation Code</label>
          <input
            id="confirmation-code"
            type="text"
            inputMode="numeric"
            maxLength={6}
            value={code()}
            onInput={(e) => setCode(e.currentTarget.value)}
            class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 border border-xcord-border focus:border-xcord-brand focus:outline-none text-center text-2xl tracking-widest"
            placeholder="000000"
            required
          />
        </div>
        <button
          data-testid="confirm-email-submit-button"
          type="submit"
          disabled={loading() || code().length !== 6}
          class="w-full bg-xcord-brand hover:bg-xcord-brand-hover text-white font-medium py-2 rounded disabled:opacity-50"
        >
          {loading() ? 'Confirming...' : 'Confirm'}
        </button>
      </form>
    </div>
  );
}
