import { createSignal, Show, For } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './TwoFactorSetup.module.css';

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
    return { error: getErrorMessage(err, 'Failed to initiate 2FA setup'), success: false };
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
    return { error: getErrorMessage(err, 'Invalid verification code'), success: false };
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
    return { error: getErrorMessage(err, 'Invalid password'), success: false };
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
      setError(getErrorMessage(err, 'Failed to initiate 2FA setup'));
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
      setError(getErrorMessage(err, 'Invalid verification code'));
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
      setError(getErrorMessage(err, 'Invalid password'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class={styles.section}>
      <h3 class={styles.heading}>Two-Factor Authentication</h3>
      <p class={styles.description}>
        Add an extra layer of security to your account using email-based verification codes.
      </p>

      {error() && <p class={styles.errorText}>{error()}</p>}
      {success() && <p class={styles.successText}>{success()}</p>}

      <Show when={phase() === 'idle'}>
        <div class={styles.statusRow}>
          <div class={styles.statusIndicatorGroup}>
            <span
              data-testid="2fa-status-indicator"
              data-2fa-enabled={String(enabled())}
              class={enabled() ? styles.statusDotEnabled : styles.statusDotDisabled}
            />
            <span class={styles.statusLabel}>
              {enabled() ? 'Enabled' : 'Disabled'}
            </span>
          </div>

          <Show
            when={enabled()}
            fallback={
              <button
                data-testid="2fa-enable-button"
                type="button"
                disabled={loading()}
                onClick={handleEnableInit}
                class={styles.primaryButton}
              >
                {loading() ? 'Sending code...' : 'Enable 2FA'}
              </button>
            }
          >
            <button
              data-testid="2fa-disable-button"
              type="button"
              disabled={loading()}
              onClick={handleDisableInit}
              class={styles.dangerButton}
            >
              Disable 2FA
            </button>
          </Show>
        </div>
      </Show>

      <Show when={phase() === 'enable-pending'}>
        <div class={styles.phaseContainer}>
          <p class={styles.phaseText}>
            A verification code has been sent to your email address. Enter it below to complete setup.
          </p>
          <div class={styles.fieldGroup}>
            <label for="2fa-enable-code" class={styles.label}>
              Verification Code
            </label>
            <input
              id="2fa-enable-code"
              data-testid="2fa-code-input"
              type="text"
              inputmode="numeric"
              maxlength={6}
              value={code()}
              onInput={(e) => setCode(e.currentTarget.value)}
              placeholder="000000"
              class={styles.codeInput}
            />
          </div>
          <div class={styles.buttonRow}>
            <button
              data-testid="2fa-verify-button"
              type="button"
              disabled={loading()}
              onClick={handleEnableConfirm}
              class={styles.primaryButton}
            >
              {loading() ? 'Verifying...' : 'Verify'}
            </button>
            <button
              data-testid="2fa-enable-cancel-button"
              type="button"
              disabled={loading()}
              onClick={cancelFlow}
              class={styles.secondaryButton}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      <Show when={phase() === 'backup-codes'}>
        <div class={styles.backupCodesContainer}>
          <div class={styles.warningBox}>
            <p class={styles.warningTitle}>Save these backup codes now.</p>
            <p class={styles.warningBody}>
              These codes will not be shown again. Each code can only be used once. Store them somewhere safe - if you lose access to your email, you can use a backup code to sign in.
            </p>
          </div>

          <div class={styles.codesBox}>
            <div class={styles.codesGrid}>
              <For each={backupCodes()}>
                {(code) => (
                  <span class={styles.codeItem}>{code}</span>
                )}
              </For>
            </div>
          </div>

          <div class={styles.buttonRow}>
            <button
              data-testid="2fa-copy-backup-codes-button"
              type="button"
              onClick={handleCopyAll}
              class={styles.secondaryButton}
            >
              {copied() ? 'Copied!' : 'Copy all codes'}
            </button>
            <button
              data-testid="2fa-backup-codes-done-button"
              type="button"
              onClick={handleBackupCodesDone}
              class={styles.primaryButton}
            >
              I have saved these codes
            </button>
          </div>
        </div>
      </Show>

      <Show when={phase() === 'disable-confirm'}>
        <div class={styles.phaseContainer}>
          <p class={styles.phaseText}>
            Enter your current password to confirm disabling two-factor authentication.
          </p>
          <div class={styles.fieldGroup}>
            <label for="2fa-disable-password" class={styles.label}>
              Current Password
            </label>
            <input
              id="2fa-disable-password"
              data-testid="2fa-disable-password-input"
              type="password"
              value={code()}
              onInput={(e) => setCode(e.currentTarget.value)}
              placeholder="Enter your password"
              class={styles.passwordInput}
            />
          </div>
          <div class={styles.buttonRow}>
            <button
              data-testid="2fa-disable-confirm-button"
              type="button"
              disabled={loading()}
              onClick={handleDisableConfirm}
              class={styles.dangerButton}
            >
              {loading() ? 'Disabling...' : 'Disable 2FA'}
            </button>
            <button
              data-testid="2fa-disable-cancel-button"
              type="button"
              disabled={loading()}
              onClick={cancelFlow}
              class={styles.secondaryButton}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>
    </div>
  );
}
