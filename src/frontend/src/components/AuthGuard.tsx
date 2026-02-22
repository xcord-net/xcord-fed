import { ParentProps, Show, createEffect } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useAuth } from '../stores/auth.store';

/**
 * Wraps protected routes and redirects unauthenticated users to /login.
 * Waits for the initial auth validation to complete before rendering.
 */
export default function AuthGuard(props: ParentProps) {
  const auth = useAuth();
  const navigate = useNavigate();

  createEffect(() => {
    // Only act once the initial auth check is complete
    if (!auth.isLoading && !auth.isAuthenticated) {
      navigate('/login', { replace: true });
    }
  });

  return (
    <Show when={!auth.isLoading && auth.isAuthenticated}>
      {props.children}
    </Show>
  );
}
