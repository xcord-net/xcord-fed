import { createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import { createFocusTrap } from '../hooks/createFocusTrap';

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

  let dialogRef!: HTMLDivElement;
  let copyTimeoutId: ReturnType<typeof setTimeout> | undefined;

  createFocusTrap(() => dialogRef, { onEscape: () => props.onClose() });

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
      const e = err as { detail?: string; message?: string };
      setError(e?.detail || e?.message || 'Failed to create invite');
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
      // Clipboard write failed — silently ignore
    }
  };

  const handleGenerateNew = () => {
    createInvite();
  };

  return (
    <div
      class="fixed inset-0 bg-black/70 flex items-center justify-center z-50"
      onClick={(e) => { if (e.target === e.currentTarget) props.onClose(); }}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-label="Invite People"
        class="bg-xcord-bg-secondary rounded-lg shadow-xl w-full max-w-md p-6"
      >
        <h2 class="text-xl font-bold text-xcord-text-primary mb-1">Invite People</h2>
        <p class="text-xcord-text-secondary text-sm mb-4">Share this link to invite people to your server.</p>

        {/* Expiry and max uses options */}
        <div class="flex gap-3 mb-4">
          <div class="flex-1">
            <label for="invite-expiry" class="block text-xcord-text-secondary text-xs font-semibold uppercase tracking-wide mb-1">
              Expire after
            </label>
            <select
              id="invite-expiry"
              class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-2 py-1.5 text-sm border border-xcord-border focus:border-xcord-brand focus-visible:ring-2 focus-visible:ring-xcord-brand focus:outline-none"
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

          <div class="flex-1">
            <label for="invite-max-uses" class="block text-xcord-text-secondary text-xs font-semibold uppercase tracking-wide mb-1">
              Max uses
            </label>
            <select
              id="invite-max-uses"
              class="w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-2 py-1.5 text-sm border border-xcord-border focus:border-xcord-brand focus-visible:ring-2 focus-visible:ring-xcord-brand focus:outline-none"
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
        <div class="mb-4">
          <label class="block text-xcord-text-secondary text-xs font-semibold uppercase tracking-wide mb-1">
            Invite Link
          </label>
          <div class="flex items-center gap-2">
            <input
              type="text"
              readonly
              value={loading() ? 'Generating...' : (inviteLink() || (error() ? 'Failed to generate' : ''))}
              aria-label="Invite link"
              class="flex-1 bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-2 text-sm border border-xcord-border focus:outline-none select-all cursor-text"
              onClick={(e) => e.currentTarget.select()}
            />
            <button
              type="button"
              onClick={handleCopy}
              disabled={!invite() || loading()}
              aria-label={copied() ? 'Copied!' : 'Copy invite link'}
              class="px-3 py-2 bg-xcord-brand hover:bg-xcord-brand-hover text-white text-sm font-medium rounded disabled:opacity-50 transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none whitespace-nowrap"
            >
              {copied() ? 'Copied!' : 'Copy'}
            </button>
          </div>
        </div>

        {/* Error */}
        {error() && (
          <p role="alert" class="text-red-400 text-sm mb-4">{error()}</p>
        )}

        {/* Actions */}
        <div class="flex justify-between items-center">
          <button
            type="button"
            onClick={handleGenerateNew}
            disabled={loading()}
            class="text-xcord-text-secondary hover:text-xcord-text-primary text-sm transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none rounded disabled:opacity-50"
          >
            Generate New Link
          </button>
          <button
            type="button"
            onClick={() => props.onClose()}
            class="px-4 py-2 bg-xcord-bg-primary hover:bg-xcord-bg-tertiary text-xcord-text-primary text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
          >
            Done
          </button>
        </div>
      </div>
    </div>
  );
}
