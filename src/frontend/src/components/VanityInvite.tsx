import { Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';

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
    <div class="flex flex-col bg-xcord-bg-secondary rounded-lg">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-bg-tertiary flex items-center justify-between">
        <h2 class="text-xcord-text-primary font-semibold">Vanity Invite URL</h2>
        <Show when={props.isOwner && !isEditing()}>
          <button
            class="text-xcord-brand hover:underline text-sm"
            onClick={() => setIsEditing(true)}
            aria-label="Edit Vanity URL"
          >
            Edit
          </button>
        </Show>
      </div>

      <div class="px-4 py-4 space-y-4">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-16">
            <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
          </div>
        </Show>

        <Show when={!isLoading()}>
          {/* Success banner */}
          <Show when={successMessage()}>
            <div class="bg-green-600/20 text-green-400 text-sm px-3 py-2 rounded" role="status">
              {successMessage()}
            </div>
          </Show>

          {/* Current vanity URL display */}
          <Show when={vanityInfo()?.slug}>
            <div class="space-y-2">
              <p class="text-xcord-text-muted text-xs font-medium uppercase tracking-wide">
                Current Vanity URL
              </p>
              <div class="flex items-center gap-2 bg-xcord-bg-tertiary rounded px-3 py-2">
                <span class="text-xcord-text-primary text-sm flex-1 font-mono truncate">
                  {buildVanityUrl(vanityInfo()!.slug!)}
                </span>
                <button
                  class="flex-shrink-0 px-2.5 py-1 rounded text-xs font-medium bg-xcord-bg-secondary text-xcord-text-muted hover:text-xcord-text-primary transition-colors"
                  onClick={handleCopy}
                  aria-label="Copy vanity URL"
                >
                  {copied() ? 'Copied!' : 'Copy'}
                </button>
              </div>
            </div>
          </Show>

          <Show when={!vanityInfo()?.slug && !isEditing()}>
            <p class="text-xcord-text-muted text-sm">
              No vanity URL set.{' '}
              <Show when={props.isOwner}>
                <button
                  class="text-xcord-brand hover:underline"
                  onClick={() => setIsEditing(true)}
                >
                  Set one now
                </button>
              </Show>
            </p>
          </Show>

          {/* Edit form */}
          <Show when={isEditing() && props.isOwner}>
            <div class="space-y-3">
              <div>
                <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
                  Custom Slug
                </label>
                <div class="flex items-center bg-xcord-bg-tertiary rounded overflow-hidden">
                  <span class="px-3 py-2 text-xcord-text-muted text-sm select-none">/invite/</span>
                  <input
                    type="text"
                    class="flex-1 bg-transparent text-xcord-text-primary text-sm py-2 pr-3 outline-none"
                    placeholder="my-server"
                    value={slugInput()}
                    onInput={(e) => handleSlugInput(e.currentTarget.value)}
                    aria-label="Vanity URL slug"
                    aria-describedby="slug-hint"
                  />
                </div>
                <p id="slug-hint" class="text-xcord-text-muted text-xs mt-1">
                  3–32 characters: letters, numbers, and hyphens only.
                </p>
              </div>

              <Show when={validationError()}>
                <p class="text-red-400 text-xs" role="alert">
                  {validationError()}
                </p>
              </Show>

              <Show when={submitError()}>
                <p class="text-red-400 text-xs" role="alert">
                  {submitError()}
                </p>
              </Show>

              <div class="flex gap-2">
                <button
                  class="px-4 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand-hover transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                  onClick={handleSave}
                  disabled={isSubmitting() || validationError() !== null}
                  aria-label="Save Vanity URL"
                >
                  {isSubmitting() ? 'Saving...' : 'Save'}
                </button>
                <button
                  class="px-4 py-2 bg-xcord-bg-tertiary text-xcord-text-muted text-sm rounded hover:bg-xcord-bg-primary hover:text-xcord-text-primary transition-colors"
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
