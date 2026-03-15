import { createSignal, For, Show } from 'solid-js';
import { usePresence } from '../stores/presence.store';
import { useAuth } from '../stores/auth.store';
import { useSignalR } from '../stores/signalr.store';
import type { PresenceStatus } from '../types/presence';
import { statusColorMap } from './PresenceDot';
import Menu from './ui/Menu';

const STATUS_OPTIONS: { status: PresenceStatus; label: string }[] = [
  { status: 'online', label: 'Online' },
  { status: 'idle', label: 'Idle' },
  { status: 'dnd', label: 'Do Not Disturb' },
  { status: 'offline', label: 'Invisible' },
];

export default function StatusPicker() {
  const [isOpen, setIsOpen] = createSignal(false);
  const [isSaving, setIsSaving] = createSignal(false);
  let triggerRef!: HTMLButtonElement;
  const presence = usePresence();
  const auth = useAuth();
  const signalR = useSignalR();

  const currentStatus = () => {
    const userId = auth.user?.id;
    if (!userId) return 'online' as PresenceStatus;
    return presence.getPresence(userId);
  };

  async function selectStatus(status: PresenceStatus) {
    setIsSaving(true);
    try {
      await signalR.updatePresence(status);
      const userId = auth.user?.id;
      if (userId) {
        presence.updatePresence(userId, status);
      }
    } catch {
      // Silently fail - presence is non-critical
    } finally {
      setIsSaving(false);
      setIsOpen(false);
    }
  }

  return (
    <div class="relative">
      <button
        data-testid="nav-set-status-button"
        ref={triggerRef}
        type="button"
        aria-label="Set status"
        aria-haspopup="true"
        aria-expanded={isOpen()}
        onClick={() => setIsOpen(!isOpen())}
        class="w-4 h-4 rounded-full border-2 border-xcord-bg-tertiary absolute -bottom-0.5 -right-0.5 cursor-pointer"
        classList={{
          [statusColorMap[currentStatus()]]: true,
        }}
      />

      <Menu
        open={isOpen()}
        onClose={() => { setIsOpen(false); triggerRef?.focus(); }}
        anchorRef={triggerRef}
        placement="top-start"
      >
        <For each={STATUS_OPTIONS}>
          {(option) => (
            <button
              type="button"
              role="menuitem"
              aria-label={`Set status to ${option.status}`}
              disabled={isSaving()}
              onClick={() => selectStatus(option.status)}
              class="w-full px-3 py-2 text-left text-sm text-xcord-text-secondary hover:bg-xcord-bg-primary hover:text-white transition-colors flex items-center gap-2.5 disabled:opacity-50"
            >
              <span
                class={`w-2.5 h-2.5 rounded-full flex-shrink-0 ${statusColorMap[option.status]}`}
                aria-hidden="true"
              />
              <span>{option.label}</span>
              <Show when={currentStatus() === option.status}>
                <span class="ml-auto text-xcord-text-muted text-xs" aria-hidden="true">&check;</span>
              </Show>
            </button>
          )}
        </For>
      </Menu>
    </div>
  );
}
