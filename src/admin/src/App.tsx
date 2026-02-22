import { createSignal, Show, onMount } from 'solid-js';
import { useAuth } from './stores/auth.store';
import { Login } from './components/Login';
import { Layout } from './components/Layout';
import { InstanceOverview } from './components/InstanceOverview';
import { BotManagement } from './components/BotManagement';
import { WebhookOverview } from './components/WebhookOverview';

type Page = 'login' | 'overview' | 'bots' | 'webhooks';

export function App() {
  const auth = useAuth();
  const [currentPage, setCurrentPage] = createSignal<Page>('login');

  onMount(async () => {
    const isValid = await auth.validateAuth();
    if (isValid) {
      setCurrentPage('overview');
    } else {
      setCurrentPage('login');
    }
  });

  const handleNavigate = (page: string) => {
    if (page === 'overview' || page === 'bots' || page === 'webhooks') {
      setCurrentPage(page as Page);
    }
  };

  return (
    <Show
      when={auth.isLoading}
      fallback={
        <Show
          when={auth.isAuthenticated && auth.isAdmin}
          fallback={<Login />}
        >
          <Layout
            currentPage={currentPage() === 'login' ? 'overview' : currentPage() as 'overview' | 'bots' | 'webhooks'}
            onNavigate={handleNavigate}
          >
            <Show when={currentPage() === 'overview'}>
              <InstanceOverview />
            </Show>

            <Show when={currentPage() === 'bots'}>
              <BotManagement />
            </Show>

            <Show when={currentPage() === 'webhooks'}>
              <WebhookOverview />
            </Show>
          </Layout>
        </Show>
      }
    >
      <div class="min-h-screen flex items-center justify-center">
        <div class="text-lg">Loading...</div>
      </div>
    </Show>
  );
}
