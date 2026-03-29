import { createSignal, For, Show } from 'solid-js';
import { usePresence } from '../stores/presence.store';
import { useAuth } from '../stores/auth.store';
import { useSignalR } from '../stores/signalr.store';
import type { PresenceStatus } from '../types/presence';
import { statusClassMap } from './PresenceDot';
import Menu from './ui/Menu';
import styles from './StatusPicker.module.css';

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
    <div class={styles.wrapper}>
      <button
        data-testid="nav-set-status-button"
        ref={triggerRef}
        type="button"
        aria-label="Set status"
        aria-haspopup="true"
        aria-expanded={isOpen()}
        onClick={() => setIsOpen(!isOpen())}
        class={styles.triggerButton}
        classList={{
          [statusClassMap[currentStatus()]]: true,
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
              class={styles.menuItem}
            >
              <span
                class={`${styles.statusDot} ${statusClassMap[option.status]}`}
                aria-hidden="true"
              />
              <span>{option.label}</span>
              <Show when={currentStatus() === option.status}>
                <span class={styles.checkmark} aria-hidden="true">&check;</span>
              </Show>
            </button>
          )}
        </For>
      </Menu>
    </div>
  );
}
