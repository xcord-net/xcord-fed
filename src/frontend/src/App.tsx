import { Router, Route } from '@solidjs/router';
import { ErrorBoundary, Show, lazy, onMount } from 'solid-js';
import AuthGuard from './components/AuthGuard';
import { useAuth } from './stores/auth.store';
import { useSignalR } from './stores/signalr.store';
import styles from './App.module.css';

const Login = lazy(() => import('./pages/Login'));
const Register = lazy(() => import('./pages/Register'));
const ConfirmEmail = lazy(() => import('./pages/ConfirmEmail'));
const ForgotPassword = lazy(() => import('./pages/ForgotPassword'));
const ResetPassword = lazy(() => import('./pages/ResetPassword'));
const Layout = lazy(() => import('./components/Layout'));
const JoinInvite = lazy(() => import('./pages/JoinInvite'));

function AppErrorFallback(err: unknown) {
  const message = err instanceof Error ? err.message : String(err);
  return (
    <div class={styles.errorFallback}>
      <p class={styles.errorMessage}>Something went wrong</p>
      <p class={styles.errorDetail}>{message}</p>
      <button
        class={styles.errorReloadButton}
        onClick={() => window.location.reload()}
      >
        Reload
      </button>
    </div>
  );
}

function ProtectedLayout() {
  return (
    <AuthGuard>
      <Layout />
    </AuthGuard>
  );
}

export default function App() {
  const auth = useAuth();
  const signalR = useSignalR();

  onMount(() => {
    auth.validateAuth();
  });

  return (
    <ErrorBoundary fallback={AppErrorFallback}>
      {/* Suspension overlay - shown when the server sends System_ShuttingDown */}
      <Show when={signalR.suspensionReason !== null}>
        <div role="alert" aria-live="assertive" aria-label="Server suspended" class={styles.suspensionOverlay}>
          <div class={styles.suspensionContent}>
            <svg aria-hidden="true" class={styles.suspensionIcon} xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
            </svg>
            <p class={styles.suspensionTitle}>Server Suspended</p>
            <p class={styles.suspensionBody}>
              This server has been temporarily suspended. You have been disconnected.
            </p>
            <p class={styles.suspensionReason}>
              Reason: {signalR.suspensionReason}
            </p>
          </div>
        </div>
      </Show>
      <Router>
        <Route path="/login" component={Login} />
        <Route path="/register" component={Register} />
        <Route path="/confirm-email" component={ConfirmEmail} />
        <Route path="/forgot-password" component={ForgotPassword} />
        <Route path="/reset-password" component={ResetPassword} />
        <Route path="/invite/:code" component={JoinInvite} />
        <Route path="/channels/:serverId/:channelId" component={ProtectedLayout} />
        <Route path="/channels/:serverId" component={ProtectedLayout} />
        <Route path="/" component={ProtectedLayout} />
      </Router>
    </ErrorBoundary>
  );
}
