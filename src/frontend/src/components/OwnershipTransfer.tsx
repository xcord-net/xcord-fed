import { createSignal, For, Show } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import Modal from './ui/Modal';
import styles from './OwnershipTransfer.module.css';
import EmptyState from './ui/EmptyState';

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
      setError(getErrorMessage(err, 'Failed to load members'));
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
      setError(getErrorMessage(err, 'Failed to transfer ownership'));
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
      <div class={styles.section} data-testid="ownership-transfer-section">
        <h3 class={styles.sectionTitle}>
          Server Ownership
        </h3>
        <p class={styles.sectionDescription}>
          Transfer server ownership to another member. You will lose owner
          privileges.
        </p>
        <button
          class={styles.openButton}
          data-testid="transfer-ownership-open-button"
          onClick={handleOpenDialog}
        >
          Transfer Ownership
        </button>

        {/* Step 1: Select Member */}
        <Modal open={step() === 'select-member'} onClose={handleClose} title="Select New Owner" size="md">
          <div class={styles.modalBody} data-testid="transfer-select-member-dialog">
            <Show when={error()}>
              <div class={styles.errorBanner}>{error()}</div>
            </Show>

            <Show when={isLoading()}>
              <p class={styles.loadingText}>Loading members...</p>
            </Show>

            <Show when={!isLoading()}>
              <div class={styles.memberList} data-testid="transfer-member-list">
                <For each={members()}>
                  {(member) => (
                    <button
                      class={styles.memberButton}
                      data-testid={`transfer-member-option-${member.userId}`}
                      onClick={() => handleSelectMember(member)}
                      aria-pressed={selectedMember()?.userId === member.userId}
                    >
                      <div class={styles.memberAvatar}>
                        {(member.displayName || member.username).charAt(0).toUpperCase()}
                      </div>
                      <div>
                        <p class={styles.memberDisplayName}>
                          {member.displayName || member.username}
                        </p>
                        <p class={styles.memberUsername}>
                          @{member.username}
                        </p>
                      </div>
                    </button>
                  )}
                </For>
                <Show when={members().length === 0}>
                  <EmptyState
                    title="Nobody to transfer to"
                    body="You are the only member. Invite someone before handing over the community."
                    dense
                    data-testid="ownership-transfer-empty"
                  />
                </Show>
              </div>
            </Show>

            <button
              class={styles.cancelButton}
              data-testid="transfer-cancel-button"
              onClick={handleClose}
            >
              Cancel
            </button>
          </div>
        </Modal>

        {/* Step 2: Confirm with server name */}
        <Modal open={step() === 'confirm-name'} onClose={handleClose} title="Confirm Ownership Transfer" size="md">
          <div class={styles.modalBody} data-testid="transfer-confirm-dialog">
            <p class={styles.confirmDescription}>
              You are about to transfer ownership of this server to{' '}
              <strong class={styles.confirmHighlight}>
                {selectedMember()?.displayName || selectedMember()?.username}
              </strong>
              . Type the server name{' '}
              <strong class={styles.confirmHighlight}>{props.serverName}</strong> to
              confirm.
            </p>

            <Show when={error()}>
              <div class={styles.errorBanner}>{error()}</div>
            </Show>

            <div class={styles.fieldGroup}>
              <label class={styles.fieldLabel}>
                Server Name
              </label>
              <input
                type="text"
                class={styles.textInput}
                data-testid="transfer-server-name-input"
                value={serverNameInput()}
                onInput={(e) => setServerNameInput(e.currentTarget.value)}
                placeholder={props.serverName}
              />
            </div>

            <div class={styles.actionRow}>
              <button
                class={styles.confirmButton}
                data-testid="transfer-confirm-button"
                onClick={handleConfirmTransfer}
                disabled={isLoading() || serverNameInput().trim() !== props.serverName}
              >
                {isLoading() ? 'Transferring...' : 'Confirm Transfer'}
              </button>
              <button
                class={styles.cancelButton}
                data-testid="transfer-confirm-cancel-button"
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
