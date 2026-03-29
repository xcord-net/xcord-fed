import { Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './VanityInvite.module.css';

// ---- Types ----

export interface VanityUrlInfo {
  serverId: string;
  slug: string | null;
  vanityUrl: string | null;
}

interface VanityInviteProps {
  serverId: string;
  isOwner?: boolean;
}

// ---- Pure helpers ----

export function validateVanitySlug(slug: string): string | null {
  const trimmed = slug.trim();
  if (trimmed.length === 0) return 'Slug is required.';
  if (trimmed.length < 3) return 'Slug must be at least 3 characters.';
  if (trimmed.length > 32) return 'Slug must be 32 characters or fewer.';
  if (!/^[a-zA-Z0-9-]+$/.test(trimmed)) {
    return 'Slug may only contain letters, numbers, and hyphens.';
  }
  if (trimmed.startsWith('-') || trimmed.endsWith('-')) {
    return 'Slug must not start or end with a hyphen.';
  }
  return null;
}

export function buildVanityUrl(slug: string): string {
  return `/invite/${slug}`;
}

// ---- Component ----

export default function VanityInvite(props: VanityInviteProps) {
  const [vanityInfo, setVanityInfo] = createSignal<VanityUrlInfo | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);
  const [isEditing, setIsEditing] = createSignal(false);
  const [isSubmitting, setIsSubmitting] = createSignal(false);
  const [slugInput, setSlugInput] = createSignal('');
  const [validationError, setValidationError] = createSignal<string | null>(null);
  const [submitError, setSubmitError] = createSignal<string | null>(null);
  const [successMessage, setSuccessMessage] = createSignal<string | null>(null);
  const [copied, setCopied] = createSignal(false);

  const loadVanityInfo = async (serverId: string) => {
    setIsLoading(true);
    try {
      const data = await api.get<VanityUrlInfo>(`/api/v1/servers/${serverId}/vanity-url`);
      setVanityInfo(data);
      if (data.slug) {
        setSlugInput(data.slug);
      }
    } catch {
      setVanityInfo(null);
    } finally {
      setIsLoading(false);
    }
  };

  createEffect(() => {
    const serverId = props.serverId;
    if (serverId) {
      loadVanityInfo(serverId);
    }
  });

  const handleSlugInput = (value: string) => {
    setSlugInput(value);
    const err = validateVanitySlug(value);
    setValidationError(err);
  };

  const handleSave = async () => {
    const err = validateVanitySlug(slugInput());
    if (err) {
      setValidationError(err);
      return;
    }

    setIsSubmitting(true);
    setSubmitError(null);
    try {
      const updated = await api.put<VanityUrlInfo>(
        `/api/v1/servers/${props.serverId}/vanity-url`,
        { slug: slugInput().trim().toLowerCase() },
      );
      setVanityInfo(updated);
      setIsEditing(false);
      setSuccessMessage('Vanity URL saved.');
      setTimeout(() => setSuccessMessage(null), 3000);
    } catch (err: unknown) {
      setSubmitError(getErrorMessage(err, 'Failed to save vanity URL. Please try again.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCopy = async () => {
    const info = vanityInfo();
    if (!info?.vanityUrl) return;

    try {
      await navigator.clipboard.writeText(
        typeof window !== 'undefined' ? `${window.location.origin}${info.vanityUrl}` : info.vanityUrl,
      );
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard access may be denied
    }
  };

  const handleCancelEdit = () => {
    const info = vanityInfo();
    setSlugInput(info?.slug ?? '');
    setValidationError(null);
    setSubmitError(null);
    setIsEditing(false);
  };

  return (
    <div class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Vanity Invite URL</h2>
        <Show when={props.isOwner && !isEditing()}>
          <button
            class={styles.editLink}
            onClick={() => setIsEditing(true)}
            aria-label="Edit Vanity URL"
          >
            Edit
          </button>
        </Show>
      </div>

      <div class={styles.body}>
        <Show when={isLoading()}>
          <div class={styles.spinnerWrapper}>
            <div class={styles.spinner} />
          </div>
        </Show>

        <Show when={!isLoading()}>
          {/* Success banner */}
          <Show when={successMessage()}>
            <div class={styles.successBanner} role="status">
              {successMessage()}
            </div>
          </Show>

          {/* Current vanity URL display */}
          <Show when={vanityInfo()?.slug}>
            <div class={styles.currentUrlSection}>
              <p class={styles.currentUrlLabel}>
                Current Vanity URL
              </p>
              <div class={styles.currentUrlRow}>
                <span class={styles.currentUrlValue}>
                  {buildVanityUrl(vanityInfo()!.slug!)}
                </span>
                <button
                  class={styles.copyButton}
                  onClick={handleCopy}
                  aria-label="Copy vanity URL"
                >
                  {copied() ? 'Copied!' : 'Copy'}
                </button>
              </div>
            </div>
          </Show>

          <Show when={!vanityInfo()?.slug && !isEditing()}>
            <p class={styles.noVanityText}>
              No vanity URL set.{' '}
              <Show when={props.isOwner}>
                <button
                  class={styles.setNowButton}
                  onClick={() => setIsEditing(true)}
                >
                  Set one now
                </button>
              </Show>
            </p>
          </Show>

          {/* Edit form */}
          <Show when={isEditing() && props.isOwner}>
            <div class={styles.editForm}>
              <div>
                <label class={styles.fieldLabel}>
                  Custom Slug
                </label>
                <div class={styles.slugInputRow}>
                  <span class={styles.slugPrefix}>/invite/</span>
                  <input
                    type="text"
                    class={styles.slugInput}
                    placeholder="my-server"
                    value={slugInput()}
                    onInput={(e) => handleSlugInput(e.currentTarget.value)}
                    aria-label="Vanity URL slug"
                    aria-describedby="slug-hint"
                  />
                </div>
                <p id="slug-hint" class={styles.slugHint}>
                  3-32 characters: letters, numbers, and hyphens only.
                </p>
              </div>

              <Show when={validationError()}>
                <p class={styles.validationError} role="alert">
                  {validationError()}
                </p>
              </Show>

              <Show when={submitError()}>
                <p class={styles.submitError} role="alert">
                  {submitError()}
                </p>
              </Show>

              <div class={styles.editActions}>
                <button
                  class={styles.saveButton}
                  onClick={handleSave}
                  disabled={isSubmitting() || validationError() !== null}
                  aria-label="Save Vanity URL"
                >
                  {isSubmitting() ? 'Saving...' : 'Save'}
                </button>
                <button
                  class={styles.cancelButton}
                  onClick={handleCancelEdit}
                >
                  Cancel
                </button>
              </div>
            </div>
          </Show>
        </Show>
      </div>
    </div>
  );
}

export { VanityInvite };
