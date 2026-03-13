import { createSignal, Show, For } from 'solid-js';
import { api } from '../api/client';

type TwoFactorPhase = 'idle' | 'enable-pending' | 'backup-codes' | 'disable-confirm';

interface TwoFactorSetupProps {
  twoFactorEnabled?: boolean;
}

interface ConfirmEnableResponse {
  enabled: boolean;
  backupCodes: string[];
}

// ---- Exported pure API functions for testing ----

export async function enableTwoFactor(): Promise<{ error: string; success: boolean }> {
  try {
    await api.post('/api/v1/auth/2fa/enable');
    return { error: '', success: true };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { error: errObj?.error || 'Failed to initiate 2FA setup', success: false };
  }
}

export async function confirmEnableTwoFactor(
  code: string,
): Promise<{ error: string; success: boolean }> {
  if (!code.trim()) {
    return { error: 'Please enter the verification code', success: false };
  }
  try {
    await api.post('/api/v1/auth/2fa/confirm-enable', { code: code.trim() });
    return { error: '', success: true };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { error: errObj?.error || 'Invalid verification code', success: false };
  }
}

export async function disableTwoFactor(
  password: string,
): Promise<{ error: string; success: boolean }> {
  if (!password.trim()) {
    return { error: 'Please enter your current password', success: false };
  }
  try {
    await api.post('/api/v1/auth/2fa/disable', { currentPassword: password.trim() });
    return { error: '', success: true };
  } catch (err: unknown) {
    const errObj = err as { error?: string };
    return { error: errObj?.error || 'Invalid password', success: false };
  }
}

export default function TwoFactorSetup(props: TwoFactorSetupProps) {
  const [phase, setPhase] = createSignal<TwoFactorPhase>('idle');
  const [code, setCode] = createSignal('');
  const [error, setError] = createSignal('');
  const [success, setSuccess] = createSignal('');
  const [loading, setLoading] = createSignal(false);
  const [enabled, setEnabled] = createSignal(props.twoFactorEnabled ?? false);
  const [backupCodes, setBackupCodes] = createSignal<string[]>([]);
  const [copied, setCopied] = createSignal(false);

  const cancelFlow = () => {
    setCode('');
    setError('');
    setLoading(false);
    setPhase('idle');
  };

  const handleEnableInit = async () => {
    setError('');
    setSuccess('');
    setLoading(true);
    try {
      await api.post('/api/v1/auth/2fa/enable');
      setPhase('enable-pending');
    } catch (err: unknown) {
      const errObj = err as { error?: string };
      setError(errObj?.error || 'Failed to initiate 2FA setup');
    } finally {
      setLoading(false);
    }
  };

  const handleEnableConfirm = async () => {
    if (!code().trim()) {
      setError('Please enter the verification code');
      return;
    }
    setError('');
    setLoading(true);
    try {
      const response = await api.post<ConfirmEnableResponse>('/api/v1/auth/2fa/confirm-enable', { code: code().trim() });
      setEnabled(true);
      setCode('');
      setBackupCodes(response.backupCodes ?? []);
      setPhase('backup-codes');
    } catch (err: unknown) {
      const errObj = err as { error?: string };
      setError(errObj?.error || 'Invalid verification code');
    } finally {
      setLoading(false);
    }
  };

  const handleCopyAll = async () => {
    const codesText = backupCodes().join('\n');
    try {
      await navigator.clipboard.writeText(codesText);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard API not available - ignore
    }
  };

  const handleBackupCodesDone = () => {
    setBackupCodes([]);
    setCopied(false);
    setPhase('idle');
    setSuccess('Two-factor authentication enabled successfully. Keep your backup codes safe.');
  };

  const handleDisableInit = () => {
    setError('');
    setSuccess('');
    setCode('');
    setPhase('disable-confirm');
  };

  const handleDisableConfirm = async () => {
    if (!code().trim()) {
      setError('Please enter your current password');
      return;
    }
    setError('');
    setLoading(true);
    try {
      await api.post('/api/v1/auth/2fa/disable', { currentPassword: code().trim() });
      setEnabled(false);
      setCode('');
      setPhase('idle');
      setSuccess('Two-factor authentication disabled successfully');
    } catch (err: unknown) {
      const errObj = err as { error?: string };
      setError(errObj?.error || 'Invalid password');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="border-t border-xcord-border pt-6 mt-6">
      <h3 class="text-white font-semibold mb-1">Two-Factor Authentication</h3>
      <p class="text-xcord-text-secondary text-sm mb-4">
        Add an extra layer of security to your account using email-based verification codes.
      </p>

      {error() && <p class="text-red-400 text-sm mb-3">{error()}</p>}
      {success() && <p class="text-green-400 text-sm mb-3">{success()}</p>}

      <Show when={phase() === 'idle'}>
        <div class="flex items-center justify-between">
          <div class="flex items-center space-x-2">
            <span
              class={`inline-block w-2 h-2 rounded-full ${enabled() ? 'bg-green-400' : 'bg-xcord-text-muted'}`}
            />
            <span class="text-xcord-text-secondary text-sm">
              {enabled() ? 'Enabled' : 'Disabled'}
            </span>
          </div>

          <Show
            when={enabled()}
            fallback={
              <button
                type="button"
                disabled={loading()}
                onClick={handleEnableInit}
                class="px-4 py-2 bg-xcord-brand hover:bg-xcord-brand-hover text-white rounded font-medium disabled:opacity-50"
              >
                {loading() ? 'Sending code...' : 'Enable 2FA'}
              </button>
            }
          >
            <button
              type="button"
              disabled={loading()}
              onClick={handleDisableInit}
              class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded font-medium disabled:opacity-50"
            >
              Disable 2FA
            </button>
          </Show>
        </div>
      </Show>

      <Show when={phase() === 'enable-pending'}>
        <div class="space-y-3">
          <p class="text-xcord-text-secondary text-sm">
            A verification code has been sent to your email address. Enter it below to complete setup.
          </p>
          <div>
            <label for="2fa-enable-code" class="block text-xcord-text-secondary text-sm font-medium mb-1">
              Verification Code
            </label>
            <input
              id="2fa-enable-code"
              type="text"
              inputmode="numeric"
              maxlength={6}
              value={code()}
              onInput={(e) => setCode(e.currentTarget.value)}
              placeholder="000000"
              class="w-full px-3 py-2 bg-xcord-bg-tertiary text-xcord-text-primary rounded border border-xcord-border focus:border-xcord-brand focus:outline-none tracking-widest text-center"
            />
          </div>
          <div class="flex space-x-2">
            <button
              type="button"
              disabled={loading()}
              onClick={handleEnableConfirm}
              class="px-4 py-2 bg-xcord-brand hover:bg-xcord-brand-hover text-white rounded font-medium disabled:opacity-50"
            >
              {loading() ? 'Verifying...' : 'Verify'}
            </button>
            <button
              type="button"
              disabled={loading()}
              onClick={cancelFlow}
              class="px-4 py-2 bg-xcord-bg-tertiary hover:bg-xcord-bg-primary text-xcord-text-secondary rounded font-medium disabled:opacity-50"
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      <Show when={phase() === 'backup-codes'}>
        <div class="space-y-4">
          <div class="bg-yellow-900/30 border border-yellow-600/40 rounded-lg p-4">
            <p class="text-yellow-300 font-semibold text-sm mb-1">Save these backup codes now.</p>
            <p class="text-yellow-200/80 text-sm">
              These codes will not be shown again. Each code can only be used once. Store them somewhere safe - if you lose access to your email, you can use a backup code to sign in.
            </p>
          </div>

          <div class="bg-xcord-bg-tertiary rounded-lg p-4 font-mono text-sm">
            <div class="grid grid-cols-2 gap-2">
              <For each={backupCodes()}>
                {(code) => (
                  <span class="text-xcord-text-primary tracking-widest">{code}</span>
                )}
              </For>
            </div>
          </div>

          <div class="flex space-x-2">
            <button
              type="button"
              onClick={handleCopyAll}
              class="px-4 py-2 bg-xcord-bg-tertiary hover:bg-xcord-bg-primary text-xcord-text-secondary rounded font-medium"
            >
              {copied() ? 'Copied!' : 'Copy all codes'}
            </button>
            <button
              type="button"
              onClick={handleBackupCodesDone}
              class="px-4 py-2 bg-xcord-brand hover:bg-xcord-brand-hover text-white rounded font-medium"
            >
              I have saved these codes
            </button>
          </div>
        </div>
      </Show>

      <Show when={phase() === 'disable-confirm'}>
        <div class="space-y-3">
          <p class="text-xcord-text-secondary text-sm">
            Enter your current password to confirm disabling two-factor authentication.
          </p>
          <div>
            <label for="2fa-disable-password" class="block text-xcord-text-secondary text-sm font-medium mb-1">
              Current Password
            </label>
            <input
              id="2fa-disable-password"
              type="password"
              value={code()}
              onInput={(e) => setCode(e.currentTarget.value)}
              placeholder="Enter your password"
              class="w-full px-3 py-2 bg-xcord-bg-tertiary text-xcord-text-primary rounded border border-xcord-border focus:border-xcord-brand focus:outline-none"
            />
          </div>
          <div class="flex space-x-2">
            <button
              type="button"
              disabled={loading()}
              onClick={handleDisableConfirm}
              class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded font-medium disabled:opacity-50"
            >
              {loading() ? 'Disabling...' : 'Disable 2FA'}
            </button>
            <button
              type="button"
              disabled={loading()}
              onClick={cancelFlow}
              class="px-4 py-2 bg-xcord-bg-tertiary hover:bg-xcord-bg-primary text-xcord-text-secondary rounded font-medium disabled:opacity-50"
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>
    </div>
  );
}
