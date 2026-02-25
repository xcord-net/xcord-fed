import { createSignal, For, Show } from 'solid-js';
import { api } from '../api/client';
import Modal from './ui/Modal';

interface Member {
  userId: string;
  username: string;
  displayName?: string;
}

interface OwnershipTransferProps {
  serverId: string;
  serverName: string;
  currentUserId: string;
  ownerId: string;
  onTransferred?: (newOwnerId: string) => void;
}

type Step = 'idle' | 'select-member' | 'confirm-name';

// ---- Pure helpers ----

export function validateServerName(serverNameInput: string, actualServerName: string): string {
  if (serverNameInput.trim() !== actualServerName) {
    return 'Server name does not match. Please type the exact server name.';
  }
  return '';
}

// ---- Component ----

export default function OwnershipTransfer(props: OwnershipTransferProps) {
  const [step, setStep] = createSignal<Step>('idle');
  const [members, setMembers] = createSignal<Member[]>([]);
  const [selectedMember, setSelectedMember] = createSignal<Member | null>(null);
  const [serverNameInput, setServerNameInput] = createSignal('');
  const [error, setError] = createSignal('');
  const [isLoading, setIsLoading] = createSignal(false);

  // Only server owner can transfer ownership
  const isOwner = () => props.currentUserId === props.ownerId;

  const loadMembers = async () => {
    setIsLoading(true);
    setError('');
    try {
      const data = await api.get<Member[]>(`/api/v1/servers/${props.serverId}/members`);
      // Exclude current user from list
      setMembers(data.filter((m) => m.userId !== props.currentUserId));
    } catch (err: unknown) {
      const errObj = err as { error?: string };
      setError(errObj?.error || 'Failed to load members');
    } finally {
      setIsLoading(false);
    }
  };

  const handleOpenDialog = async () => {
    await loadMembers();
    setStep('select-member');
    setSelectedMember(null);
    setServerNameInput('');
    setError('');
  };

  const handleSelectMember = (member: Member) => {
    setSelectedMember(member);
    setStep('confirm-name');
    setServerNameInput('');
    setError('');
  };

  const handleConfirmTransfer = async () => {
    const nameError = validateServerName(serverNameInput(), props.serverName);
    if (nameError) {
      setError(nameError);
      return;
    }

    const member = selectedMember();
    if (!member) return;

    setIsLoading(true);
    setError('');

    try {
      await api.post(`/api/v1/servers/${props.serverId}/transfer-ownership`, {
        targetUserId: member.userId,
      });
      setStep('idle');
      props.onTransferred?.(member.userId);
    } catch (err: unknown) {
      const errObj = err as { error?: string };
      setError(errObj?.error || 'Failed to transfer ownership');
    } finally {
      setIsLoading(false);
    }
  };

  const handleClose = () => {
    setStep('idle');
    setSelectedMember(null);
    setServerNameInput('');
    setError('');
  };

  return (
    <Show when={isOwner()}>
      <div class="mt-4">
        <h3 class="text-xcord-text-muted text-sm font-semibold uppercase tracking-wide mb-2">
          Server Ownership
        </h3>
        <p class="text-xcord-text-muted text-sm mb-3">
          Transfer server ownership to another member. You will lose owner
          privileges.
        </p>
        <button
          class="bg-xcord-bg-primary border border-xcord-border text-xcord-text-primary px-4 py-2 rounded hover:bg-xcord-bg-secondary/80 transition text-sm"
          onClick={handleOpenDialog}
        >
          Transfer Ownership
        </button>

        {/* Step 1: Select Member */}
        <Modal open={step() === 'select-member'} onClose={handleClose} title="Select New Owner" size="md">
          <div class="p-6">
            <Show when={error()}>
              <div class="mb-3 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">{error()}</div>
            </Show>

            <Show when={isLoading()}>
              <p class="text-xcord-text-muted text-sm">Loading members...</p>
            </Show>

            <Show when={!isLoading()}>
              <div class="space-y-2 max-h-64 overflow-y-auto mb-4">
                <For each={members()}>
                  {(member) => (
                    <button
                      class="w-full flex items-center gap-3 p-3 rounded bg-xcord-bg-primary hover:bg-xcord-bg-primary/80 transition text-left"
                      onClick={() => handleSelectMember(member)}
                    >
                      <div class="w-8 h-8 rounded-full bg-xcord-brand flex items-center justify-center text-white text-sm font-semibold">
                        {(member.displayName || member.username).charAt(0).toUpperCase()}
                      </div>
                      <div>
                        <p class="text-white text-sm font-medium">
                          {member.displayName || member.username}
                        </p>
                        <p class="text-xcord-text-muted text-xs">
                          @{member.username}
                        </p>
                      </div>
                    </button>
                  )}
                </For>
                <Show when={members().length === 0}>
                  <div class="flex flex-col items-center justify-center py-8 text-center">
                    <p class="text-xcord-text-muted text-sm">No other members to transfer to.</p>
                  </div>
                </Show>
              </div>
            </Show>

            <button
              class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
              onClick={handleClose}
            >
              Cancel
            </button>
          </div>
        </Modal>

        {/* Step 2: Confirm with server name */}
        <Modal open={step() === 'confirm-name'} onClose={handleClose} title="Confirm Ownership Transfer" size="md">
          <div class="p-6">
            <p class="text-xcord-text-muted text-sm mb-4">
              You are about to transfer ownership of this server to{' '}
              <strong class="text-white">
                {selectedMember()?.displayName || selectedMember()?.username}
              </strong>
              . Type the server name{' '}
              <strong class="text-white">{props.serverName}</strong> to
              confirm.
            </p>

            <Show when={error()}>
              <div class="mb-3 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">{error()}</div>
            </Show>

            <div class="mb-4">
              <label class="text-xs text-xcord-text-muted block mb-1">
                Server Name
              </label>
              <input
                type="text"
                class="w-full bg-xcord-bg-primary text-xcord-text-primary px-3 py-2 rounded text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand"
                value={serverNameInput()}
                onInput={(e) => setServerNameInput(e.currentTarget.value)}
                placeholder={props.serverName}
              />
            </div>

            <div class="flex gap-3">
              <button
                class="flex-1 bg-xcord-brand text-white py-2 rounded hover:bg-xcord-brand-hover transition disabled:opacity-50"
                onClick={handleConfirmTransfer}
                disabled={isLoading() || serverNameInput().trim() !== props.serverName}
              >
                {isLoading() ? 'Transferring...' : 'Confirm Transfer'}
              </button>
              <button
                class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
                onClick={handleClose}
              >
                Cancel
              </button>
            </div>
          </div>
        </Modal>
      </div>
    </Show>
  );
}
