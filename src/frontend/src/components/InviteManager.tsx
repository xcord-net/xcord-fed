import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import ConfirmationButton from './ui/ConfirmationButton';

interface Invite {
  code: string;
  serverId: string;
  createdByUserId: string | null;
  maxUses: number | null;
  uses: number;
  expiresAt: string | null;
  createdAt: string;
}

interface InviteManagerProps {
  serverId: string;
}

function formatExpiry(expiresAt: string | null): string {
  if (!expiresAt) return 'Never';
  const date = new Date(expiresAt);
  const now = new Date();
  if (date < now) return 'Expired';
  return date.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export default function InviteManager(props: InviteManagerProps) {
  const [invites, setInvites] = createSignal<Invite[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [revokingCode, setRevokingCode] = createSignal<string | null>(null);
  const [confirmingRevoke, setConfirmingRevoke] = createSignal<string | null>(null);

  async function loadInvites() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.get<Invite[]>(`/api/v1/servers/${props.serverId}/invites`);
      setInvites(result);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load invites'));
    } finally {
      setIsLoading(false);
    }
  }

  async function revokeInvite(code: string) {
    setRevokingCode(code);
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/invites/${code}`);
      setInvites(invites().filter((i) => i.code !== code));
      setConfirmingRevoke(null);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to revoke invite'));
    } finally {
      setRevokingCode(null);
    }
  }

  onMount(() => {
    loadInvites();
  });

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border">
        <h2 class="text-white font-semibold">Active Invites</h2>
      </div>

      <Show when={error()}>
        <div class="px-4 py-2 bg-red-500/20 text-red-400 text-sm">{error()}</div>
      </Show>

      <div class="flex-1 overflow-y-auto">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading invites...</p>
          </div>
        </Show>

        <Show when={!isLoading() && invites().length === 0}>
          <div class="flex flex-col items-center justify-center h-32 text-xcord-text-muted">
            <p class="text-lg font-semibold">No active invites</p>
            <p class="text-sm mt-1">Use the server menu to create invites.</p>
          </div>
        </Show>

        <For each={invites()}>
          {(invite) => (
            <div class="px-4 py-3 flex items-start justify-between hover:bg-xcord-bg-primary/50 border-b border-xcord-border">
              <div class="flex-1 min-w-0">
                <p class="text-white font-mono text-sm font-medium" data-testid={`invite-code-${invite.code}`} aria-label={`Invite code ${invite.code}`}>
                  {invite.code}
                </p>
                <p class="text-xcord-text-muted text-xs mt-0.5">
                  Uses: {invite.uses}{invite.maxUses != null ? ` / ${invite.maxUses}` : ''} &bull; Expires: {formatExpiry(invite.expiresAt)}
                </p>
              </div>

              <div class="ml-3">
                <ConfirmationButton
                  isConfirming={confirmingRevoke() === invite.code}
                  onStartConfirm={() => setConfirmingRevoke(invite.code)}
                  onConfirm={() => revokeInvite(invite.code)}
                  onCancel={() => setConfirmingRevoke(null)}
                  isLoading={revokingCode() === invite.code}
                  label="Revoke"
                  confirmText="Revoke?"
                  testId={`invite-revoke-${invite.code}`}
                />
              </div>
            </div>
          )}
        </For>
      </div>
    </div>
  );
}
