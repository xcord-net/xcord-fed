import { JSX } from 'solid-js';
import { useAuth } from '../stores/auth.store';

export type Page = 'overview' | 'bots' | 'users' | 'webhooks';

interface LayoutProps {
  children: JSX.Element;
  currentPage: Page;
  onNavigate: (page: string) => void;
}

const navItems: { id: Page; label: string }[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'users', label: 'Users' },
  { id: 'bots', label: 'Bots' },
  { id: 'webhooks', label: 'Webhooks' },
];

export function Layout(props: LayoutProps) {
  const auth = useAuth();

  const handleLogout = async () => {
    await auth.logout();
    window.location.reload();
  };

  return (
    <div class="min-h-screen bg-xcord-bg-primary text-xcord-text-primary">
      {/* Top navigation bar */}
      <nav class="bg-xcord-bg-tertiary border-b border-xcord-border px-6 py-3 flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="w-8 h-8 rounded-full bg-xcord-brand flex items-center justify-center text-white font-bold text-sm">
            X
          </div>
          <h1 class="text-lg font-semibold text-white">Instance Admin</h1>
        </div>
        <div class="flex items-center gap-4">
          <span class="text-sm text-xcord-text-secondary">{auth.username}</span>
          <button
            onClick={handleLogout}
            class="px-3 py-1.5 rounded text-sm bg-xcord-bg-input text-xcord-text-secondary hover:bg-xcord-bg-secondary hover:text-white transition-colors"
          >
            Logout
          </button>
        </div>
      </nav>

      <div class="flex">
        {/* Sidebar */}
        <aside class="w-56 min-h-[calc(100vh-52px)] bg-xcord-bg-secondary border-r border-xcord-border p-3">
          <nav class="space-y-1">
            {navItems.map((item) => (
              <button
                onClick={() => props.onNavigate(item.id)}
                class={`w-full text-left px-3 py-2 rounded text-sm font-medium transition-colors ${
                  props.currentPage === item.id
                    ? 'bg-xcord-brand/20 text-white'
                    : 'text-xcord-text-secondary hover:bg-xcord-bg-input hover:text-xcord-text-primary'
                }`}
              >
                {item.label}
              </button>
            ))}
          </nav>
        </aside>

        {/* Main content */}
        <main class="flex-1 p-6 overflow-y-auto max-h-[calc(100vh-52px)]">
          {props.children}
        </main>
      </div>
    </div>
  );
}
