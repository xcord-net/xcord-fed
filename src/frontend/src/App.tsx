import { Router, Route } from '@solidjs/router';
import { ErrorBoundary, Show, lazy, onMount } from 'solid-js';
import AuthGuard from './components/AuthGuard';
import Toaster from './components/ui/Toaster';
import { useAuth } from './stores/auth.store';
import { useSignalR } from './stores/signalr.store';
import { AlertIcon } from './components/ui/icons';
import styles from './App.module.css';

const Login = lazy(() => import('./pages/Login'));
const Register = lazy(() => import('./pages/Register'));
const ConfirmEmail = lazy(() => import('./pages/ConfirmEmail'));
const ForgotPassword = lazy(() => import('./pages/ForgotPassword'));
const ResetPassword = lazy(() => import('./pages/ResetPassword'));
const Layout = lazy(() => import('./components/Layout'));
const JoinInvite = lazy(() => import('./pages/JoinInvite'));
const NotFound = lazy(() => import('./pages/NotFound'));

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
      <Toaster />
      {/* Suspension overlay - shown when the server sends System_ShuttingDown */}
      <Show when={signalR.suspensionReason !== null}>
        <div role="alert" aria-live="assertive" aria-label="Server suspended" class={styles.suspensionOverlay}>
          <div class={styles.suspensionContent}>
            <AlertIcon class={styles.suspensionIcon} />
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
        {/* Catch-all: unknown URLs render a 404 with a way back instead of a blank screen. */}
        <Route path="*" component={NotFound} />
      </Router>
    </ErrorBoundary>
  );
}
