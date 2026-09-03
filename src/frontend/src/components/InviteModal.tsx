import { createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import Modal from './ui/Modal';
import Flexbox from './ui/Flexbox';
import { getErrorMessage } from '../utils/errors';
import styles from './InviteModal.module.css';

interface InviteModalProps {
  serverId: string;
  onClose: () => void;
}

interface Invite {
  code: string;
  serverId: string;
  maxUses: number | null;
  uses: number;
  expiresAt: string | null;
  createdAt: string;
}

const EXPIRY_OPTIONS: { label: string; value: string | undefined }[] = [
  { label: '30 minutes', value: '30m' },
  { label: '1 hour', value: '1h' },
  { label: '6 hours', value: '6h' },
  { label: '12 hours', value: '12h' },
  { label: '24 hours', value: '24h' },
  { label: '7 days', value: '7d' },
  { label: 'Never', value: undefined },
];

const MAX_USES_OPTIONS: { label: string; value: number | undefined }[] = [
  { label: 'No limit', value: undefined },
  { label: '1 use', value: 1 },
  { label: '5 uses', value: 5 },
  { label: '10 uses', value: 10 },
  { label: '25 uses', value: 25 },
  { label: '50 uses', value: 50 },
  { label: '100 uses', value: 100 },
];

function expiryToDate(value: string | undefined): string | undefined {
  if (!value) return undefined;
  const now = new Date();
  const units: Record<string, number> = { m: 60000, h: 3600000, d: 86400000 };
  const match = value.match(/^(\d+)([mhd])$/);
  if (!match) return undefined;
  const ms = parseInt(match[1], 10) * units[match[2]];
  return new Date(now.getTime() + ms).toISOString();
}

export default function InviteModal(props: InviteModalProps) {
  const [invite, setInvite] = createSignal<Invite | null>(null);
  const [loading, setLoading] = createSignal(false);
  const [error, setError] = createSignal('');
  const [copied, setCopied] = createSignal(false);
  const [expiryValue, setExpiryValue] = createSignal<string | undefined>('24h');
  const [maxUsesValue, setMaxUsesValue] = createSignal<number | undefined>(undefined);

  let copyTimeoutId: ReturnType<typeof setTimeout> | undefined;

  const createInvite = async () => {
    setLoading(true);
    setError('');
    try {
      const expiresAt = expiryToDate(expiryValue());
      const maxUses = maxUsesValue();
      const body: { maxUses?: number; expiresAt?: string } = {};
      if (maxUses !== undefined) body.maxUses = maxUses;
      if (expiresAt !== undefined) body.expiresAt = expiresAt;
      const result = await api.post<Invite>(`/api/v1/servers/${props.serverId}/invites`, body);
      setInvite(result);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to create invite'));
    } finally {
      setLoading(false);
    }
  };

  onMount(() => {
    createInvite();
  });

  const inviteLink = () => {
    const code = invite()?.code;
    if (!code) return '';
    return `${window.location.origin}/invite/${code}`;
  };

  const handleCopy = async () => {
    const link = inviteLink();
    if (!link) return;
    try {
      await navigator.clipboard.writeText(link);
      setCopied(true);
      if (copyTimeoutId !== undefined) clearTimeout(copyTimeoutId);
      copyTimeoutId = setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard write failed - silently ignore
    }
  };

  const handleGenerateNew = () => {
    createInvite();
  };

  return (
    <Modal data-testid="invite-dialog" open={true} onClose={props.onClose} title="Invite People" size="md">
      <div class={styles.body}>
        <p class={styles.description}>Share this link to invite people to your server.</p>

        {/* Expiry and max uses options */}
        <div class={styles.optionsRow}>
          <div class={styles.optionGroup}>
            <label for="invite-expiry" class={styles.optionLabel}>
              Expire after
            </label>
            <select
              id="invite-expiry"
              class={styles.select}
              value={expiryValue() ?? ''}
              onChange={(e) => {
                const v = e.currentTarget.value;
                setExpiryValue(v === '' ? undefined : v);
              }}
            >
              {EXPIRY_OPTIONS.map((opt) => (
                <option value={opt.value ?? ''}>{opt.label}</option>
              ))}
            </select>
          </div>

          <div class={styles.optionGroup}>
            <label for="invite-max-uses" class={styles.optionLabel}>
              Max uses
            </label>
            <select
              id="invite-max-uses"
              class={styles.select}
              value={maxUsesValue() !== undefined ? String(maxUsesValue()) : ''}
              onChange={(e) => {
                const v = e.currentTarget.value;
                setMaxUsesValue(v === '' ? undefined : parseInt(v, 10));
              }}
            >
              {MAX_USES_OPTIONS.map((opt) => (
                <option value={opt.value !== undefined ? String(opt.value) : ''}>{opt.label}</option>
              ))}
            </select>
          </div>
        </div>

        {/* Invite link display */}
        <div class={styles.linkSection}>
          <label class={styles.linkLabel}>
            Invite Link
          </label>
          <div class={styles.linkRow}>
            {/* The field holds the link and nothing else. It used to show
                "Generating..." and "Failed to generate" as its *value*, which
                is what a person copies and what a script reads - so a link
                copied a moment too early was the word "Generating...". Status
                belongs beside the box, not inside it. */}
            <input
              type="text"
              readonly
              data-testid="invite-link-input"
              value={inviteLink()}
              placeholder={loading() ? 'Generating...' : ''}
              aria-label="Invite link"
              aria-busy={loading()}
              class={styles.linkInput}
              onClick={(e) => e.currentTarget.select()}
            />
            <button
              type="button"
              onClick={handleCopy}
              disabled={!invite() || loading()}
              aria-label={copied() ? 'Copied!' : 'Copy invite link'}
              class={styles.copyButton}
            >
              {copied() ? 'Copied!' : 'Copy'}
            </button>
          </div>
        </div>

        {/* Error */}
        {error() && (
          <div role="alert" class={styles.errorAlert}>{error()}</div>
        )}

        {/* Actions */}
        <Flexbox justify="between" align="center">
          <button
            data-testid="invite-generate-button"
            type="button"
            onClick={handleGenerateNew}
            disabled={loading()}
            class={styles.generateButton}
          >
            Generate New Link
          </button>
          <button
            data-testid="invite-close-button"
            type="button"
            onClick={() => props.onClose()}
            class={styles.doneButton}
          >
            Done
          </button>
        </Flexbox>
      </div>
    </Modal>
  );
}
