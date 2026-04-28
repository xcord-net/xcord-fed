import { createSignal, createEffect, onMount, Show } from 'solid-js';
import { useParams, useNavigate } from '@solidjs/router';
import { useServers } from '../stores/server.store';
import { useAuth } from '../stores/auth.store';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import { sanitizeRedirect } from '../utils/redirect';
import styles from './JoinInvite.module.css';

export default function JoinInvite() {
  const params = useParams<{ code: string }>();
  const navigate = useNavigate();
  const serverStore = useServers();
  const auth = useAuth();
  const [error, setError] = createSignal('');
  const [joining, setJoining] = createSignal(false);
  const [joined, setJoined] = createSignal(false);
  let joinAttempted = false;

  onMount(() => { document.title = 'Join Server - Xcord'; });

  // Wait for auth validation to complete before checking authentication
  createEffect(() => {
    if (auth.isLoading) return;
    if (!auth.isAuthenticated) {
      const redirectPath = sanitizeRedirect(`/invite/${params.code}`);
      navigate(`/login?redirect=${encodeURIComponent(redirectPath)}`);
      return;
    }
    if (!joinAttempted) {
      joinAttempted = true;
      handleJoin();
    }
  });

  const navigateToServer = (serverId: string) => {
    // Navigate directly to the server route - the channel directory or first
    // channel will be selected by the destination page. Avoids an extra round
    // trip to fetch channels just to pick one.
    navigate(`/channels/${serverId}`, { replace: true });
  };

  const handleJoin = async () => {
    // Capture the code at handler invocation time so a navigation that fires
    // mid-handler (which clears the route param to undefined) doesn't cause us
    // to issue follow-up requests with undefined in the URL.
    const code = params.code;
    if (!code) return;
    setJoining(true);
    setError('');
    try {
      // Try joining as a regular invite code first
      const server = await serverStore.joinByInvite(code);
      setJoined(true);
      navigateToServer(server.id);
    } catch {
      // If regular invite fails, try vanity slug join
      try {
        const vanityResult = await api.post<{ serverId: string; serverName: string }>(
          `/api/v1/invite/${code}/join`
        );
        const serverId = String(vanityResult.serverId);
        setJoined(true);
        navigateToServer(serverId);
      } catch (err2: unknown) {
        setError(getErrorMessage(err2, 'Failed to join server'));
      }
    } finally {
      setJoining(false);
    }
  };

  return (
    <div class={styles.pageWrapper}>
      <div class={styles.card}>
        <Show when={joining()}>
          <p data-testid="invite-join-loading" class={styles.loadingText}>Joining server...</p>
        </Show>
        <Show when={joined()}>
          <p data-testid="invite-join-success" class={styles.successText}>Successfully joined! Redirecting...</p>
        </Show>
        <Show when={error()}>
          <div>
            <p data-testid="invite-join-error" class={styles.errorText}>{error()}</p>
            <button
              class={styles.goHomeButton}
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
