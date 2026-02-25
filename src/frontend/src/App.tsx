import { Router, Route } from '@solidjs/router';
import { ErrorBoundary, Show, lazy, onMount } from 'solid-js';
import AuthGuard from './components/AuthGuard';
import { useAuth } from './stores/auth.store';
import { useSignalR } from './stores/signalr.store';
import { requestPermission } from './services/notification.service';

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
    <div class="flex flex-col items-center justify-center h-screen bg-xcord-bg-primary text-xcord-text-primary gap-4">
      <p class="text-lg font-semibold">Something went wrong</p>
      <p class="text-sm text-xcord-text-muted">{message}</p>
      <button
        class="px-4 py-2 bg-xcord-brand text-white rounded hover:opacity-90"
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
    // Request desktop notification permission on app init. The browser will only
    // show the permission prompt once; subsequent calls are no-ops.
    requestPermission().catch(() => { /* permission denied or not supported */ });
  });

  return (
    <ErrorBoundary fallback={AppErrorFallback}>
      {/* Suspension overlay — shown when the server sends System_ShuttingDown */}
      <Show when={signalR.suspensionReason !== null}>
        <div role="alert" aria-live="assertive" aria-label="Server suspended" class="fixed inset-0 z-50 flex flex-col items-center justify-center bg-xcord-bg-primary/95 text-xcord-text-primary gap-4">
          <div class="flex flex-col items-center gap-3 max-w-md text-center px-6">
            <svg aria-hidden="true" class="w-16 h-16 text-xcord-brand" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
            </svg>
            <p class="text-xl font-semibold">Server Suspended</p>
            <p class="text-xcord-text-muted text-sm">
              This server has been temporarily suspended. You have been disconnected.
            </p>
            <p class="text-xs text-xcord-text-muted opacity-60 capitalize">
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
        <Route path="/channels/me" component={ProtectedLayout} />
        <Route path="/" component={ProtectedLayout} />
      </Router>
    </ErrorBoundary>
  );
}
