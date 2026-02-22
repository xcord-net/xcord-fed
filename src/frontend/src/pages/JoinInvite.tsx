import { createSignal, createEffect, Show } from 'solid-js';
import { useParams, useNavigate } from '@solidjs/router';
import { useServers } from '../stores/server.store';
import { useAuth } from '../stores/auth.store';
import { api } from '../api/client';

export default function JoinInvite() {
  const params = useParams<{ code: string }>();
  const navigate = useNavigate();
  const serverStore = useServers();
  const auth = useAuth();
  const [error, setError] = createSignal('');
  const [joining, setJoining] = createSignal(false);
  const [joined, setJoined] = createSignal(false);
  let joinAttempted = false;

  // Wait for auth validation to complete before checking authentication
  createEffect(() => {
    if (auth.isLoading) return;
    if (!auth.isAuthenticated) {
      navigate(`/login?redirect=/invite/${params.code}`);
      return;
    }
    if (!joinAttempted) {
      joinAttempted = true;
      handleJoin();
    }
  });

  const handleJoin = async () => {
    setJoining(true);
    setError('');
    try {
      const server = await serverStore.joinByInvite(params.code);
      setJoined(true);
      // Fetch channels to find the default channel for navigation
      try {
        const resp = await api.get<{ channels: { id: string }[] }>(`/api/v1/servers/${server.id}/channels`);
        const firstChannel = resp.channels?.[0];
        if (firstChannel) {
          navigate(`/channels/${server.id}/${firstChannel.id}`);
        } else {
          navigate(`/channels/me`);
        }
      } catch {
        navigate(`/channels/me`);
      }
    } catch (err: unknown) {
      const e = err as { detail?: string; message?: string };
      setError(e?.detail || e?.message || 'Failed to join server');
    } finally {
      setJoining(false);
    }
  };

  return (
    <div class="min-h-screen bg-xcord-bg-tertiary flex items-center justify-center">
      <div class="bg-xcord-bg-secondary p-8 rounded-lg shadow-xl w-full max-w-md text-center">
        <Show when={joining()}>
          <p class="text-xcord-text-primary">Joining server...</p>
        </Show>
        <Show when={joined()}>
          <p class="text-green-400 font-semibold">Successfully joined! Redirecting...</p>
        </Show>
        <Show when={error()}>
          <div>
            <p class="text-red-400 mb-4">{error()}</p>
            <button
              class="bg-xcord-brand text-white px-4 py-2 rounded hover:bg-xcord-brand-hover"
              onClick={() => navigate('/channels/me')}
            >
              Go Home
            </button>
          </div>
        </Show>
      </div>
    </div>
  );
}
