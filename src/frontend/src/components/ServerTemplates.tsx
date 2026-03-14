import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export interface TemplateChannel {
  name: string;
  type: 'Text' | 'Voice' | 'Forum';
  position: number;
}

export interface TemplateGroup {
  name: string;
  color?: string;
  roles: string[];
}

export interface ServerTemplate {
  id: string;
  name: string;
  description?: string;
  sourceServerId?: string;
  channels: TemplateChannel[];
  groups: TemplateGroup[];
  usageCount: number;
  createdAt: string;
}

// The backend returns channelData and roleData as JSON strings.
// This interface matches the raw API response shape.
interface RawServerTemplateResponse {
  id: string;
  name: string;
  description?: string;
  sourceServerId?: string;
  channelData?: string;
  groupData?: string;
  channels?: TemplateChannel[];
  groups?: TemplateGroup[];
  usageCount: number;
  createdAt: string;
}

function normalizeTemplate(raw: RawServerTemplateResponse): ServerTemplate {
  let channels: TemplateChannel[] = [];
  let groups: TemplateGroup[] = [];

  // If backend sent channelData/groupData as JSON strings, parse them
  if (typeof raw.channelData === 'string' && raw.channelData) {
    try { channels = JSON.parse(raw.channelData); } catch { channels = []; }
  } else if (Array.isArray(raw.channels)) {
    channels = raw.channels;
  }

  if (typeof raw.groupData === 'string' && raw.groupData) {
    try { groups = JSON.parse(raw.groupData); } catch { groups = []; }
  } else if (Array.isArray(raw.groups)) {
    groups = raw.groups;
  }

  return {
    id: String(raw.id),
    name: raw.name,
    description: raw.description,
    sourceServerId: raw.sourceServerId ? String(raw.sourceServerId) : undefined,
    channels,
    groups,
    usageCount: raw.usageCount ?? 0,
    createdAt: raw.createdAt,
  };
}

interface ServerTemplatesProps {
  serverId: string;
  isOwner?: boolean;
}

// ---- Pure helpers ----

export function validateTemplateName(name: string): string | null {
  const trimmed = name.trim();
  if (trimmed.length === 0) return 'Template name is required.';
  if (trimmed.length > 100) return 'Template name must be 100 characters or fewer.';
  return null;
}

export function templateChannelCount(template: ServerTemplate): number {
  return template.channels.length;
}

export function templateGroupCount(template: ServerTemplate): number {
  return template.groups.length;
}

// ---- Component ----

export default function ServerTemplates(props: ServerTemplatesProps) {
  const [templates, setTemplates] = createSignal<ServerTemplate[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [showSaveForm, setShowSaveForm] = createSignal(false);
  const [showCreateForm, setShowCreateForm] = createSignal(false);
  const [isSubmitting, setIsSubmitting] = createSignal(false);
  const [submitError, setSubmitError] = createSignal<string | null>(null);
  const [successMessage, setSuccessMessage] = createSignal<string | null>(null);
  const [selectedTemplate, setSelectedTemplate] = createSignal<ServerTemplate | null>(null);

  // Save template form state
  const [formName, setFormName] = createSignal('');
  const [formDescription, setFormDescription] = createSignal('');

  // Create from template form state
  const [newServerName, setNewServerName] = createSignal('');

  const loadTemplates = async () => {
    setIsLoading(true);
    try {
      const data = await api.get<RawServerTemplateResponse[]>('/api/v1/server-templates');
      setTemplates(data.map(normalizeTemplate));
    } catch {
      setTemplates([]);
    } finally {
      setIsLoading(false);
    }
  };

  createEffect(() => {
    // Re-run when serverId changes
    if (props.serverId) {
      loadTemplates();
    }
  });

  const handleSaveTemplate = async () => {
    const nameErr = validateTemplateName(formName());
    if (nameErr) {
      setSubmitError(nameErr);
      return;
    }

    setIsSubmitting(true);
    setSubmitError(null);
    try {
      const rawTemplate = await api.post<RawServerTemplateResponse>(
        `/api/v1/servers/${props.serverId}/templates`,
        {
          name: formName().trim(),
          description: formDescription().trim() || undefined,
        },
      );
      setTemplates((prev) => [...prev, normalizeTemplate(rawTemplate)]);
      setFormName('');
      setFormDescription('');
      setShowSaveForm(false);
      setSuccessMessage('Template saved successfully.');
      setTimeout(() => setSuccessMessage(null), 3000);
    } catch {
      setSubmitError('Failed to save template. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCreateFromTemplate = async () => {
    const template = selectedTemplate();
    if (!template || !newServerName().trim()) return;

    setIsSubmitting(true);
    setSubmitError(null);
    try {
      await api.post<{ id: string }>('/api/v1/servers/from-template', {
        templateId: template.id,
        serverName: newServerName().trim(),
      });
      setNewServerName('');
      setSelectedTemplate(null);
      setShowCreateForm(false);
      setSuccessMessage('Server created from template.');
      setTimeout(() => setSuccessMessage(null), 3000);
    } catch {
      setSubmitError('Failed to create server from template. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const openCreateForm = (template: ServerTemplate) => {
    setSelectedTemplate(template);
    setNewServerName('');
    setSubmitError(null);
    setShowCreateForm(true);
    setShowSaveForm(false);
  };

  const handleCancelSave = () => {
    setShowSaveForm(false);
    setFormName('');
    setFormDescription('');
    setSubmitError(null);
  };

  const handleCancelCreate = () => {
    setShowCreateForm(false);
    setNewServerName('');
    setSelectedTemplate(null);
    setSubmitError(null);
  };

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-bg-tertiary flex items-center justify-between flex-shrink-0">
        <h2 class="text-xcord-text-primary font-semibold">Server Templates</h2>
        <Show when={props.isOwner}>
          <button
            class="bg-xcord-brand text-white px-3 py-1.5 rounded hover:bg-xcord-brand-hover transition-colors text-sm"
            onClick={() => {
              setShowSaveForm(true);
              setShowCreateForm(false);
              setSubmitError(null);
            }}
            aria-label="Save as Template"
          >
            Save as Template
          </button>
        </Show>
      </div>

      {/* Success banner */}
      <Show when={successMessage()}>
        <div class="px-4 py-2 bg-green-600/20 text-green-400 text-sm flex-shrink-0">
          {successMessage()}
        </div>
      </Show>

      {/* Save Template Form */}
      <Show when={showSaveForm()}>
        <div class="px-4 py-4 bg-xcord-bg-primary border-b border-xcord-bg-tertiary space-y-3 flex-shrink-0">
          <h3 class="text-xcord-text-primary font-semibold text-sm">Save Server as Template</h3>

          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              Template Name *
            </label>
            <input
              type="text"
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
              placeholder="Template name..."
              value={formName()}
              onInput={(e) => setFormName(e.currentTarget.value)}
              aria-label="Template Name"
            />
          </div>

          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              Description
            </label>
            <textarea
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand resize-none"
              placeholder="Describe this template..."
              rows={3}
              value={formDescription()}
              onInput={(e) => setFormDescription(e.currentTarget.value)}
              aria-label="Template Description"
            />
          </div>

          <Show when={submitError()}>
            <p class="text-red-400 text-xs" role="alert">
              {submitError()}
            </p>
          </Show>

          <div class="flex gap-2">
            <button
              class="px-4 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand-hover transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              onClick={handleSaveTemplate}
              disabled={isSubmitting()}
              aria-label="Save Template"
            >
              {isSubmitting() ? 'Saving...' : 'Save Template'}
            </button>
            <button
              class="px-4 py-2 bg-xcord-bg-tertiary text-xcord-text-muted text-sm rounded hover:bg-xcord-bg-primary hover:text-xcord-text-primary transition-colors"
              onClick={handleCancelSave}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      {/* Create from Template Form */}
      <Show when={showCreateForm() && selectedTemplate()}>
        <div class="px-4 py-4 bg-xcord-bg-primary border-b border-xcord-bg-tertiary space-y-3 flex-shrink-0">
          <h3 class="text-xcord-text-primary font-semibold text-sm">
            Create Server from "{selectedTemplate()!.name}"
          </h3>

          <div>
            <label class="block text-xcord-text-muted text-xs font-medium uppercase tracking-wide mb-1">
              New Server Name *
            </label>
            <input
              type="text"
              class="w-full bg-xcord-bg-tertiary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
              placeholder="My new server..."
              value={newServerName()}
              onInput={(e) => setNewServerName(e.currentTarget.value)}
              aria-label="New Server Name"
            />
          </div>

          {/* Template preview */}
          <div class="bg-xcord-bg-tertiary rounded p-3 space-y-1">
            <p class="text-xcord-text-muted text-xs font-medium uppercase tracking-wide">
              Template Preview
            </p>
            <p class="text-xcord-text-primary text-xs">
              {templateChannelCount(selectedTemplate()!)} channel
              {templateChannelCount(selectedTemplate()!) !== 1 ? 's' : ''}
            </p>
            <p class="text-xcord-text-primary text-xs">
              {templateGroupCount(selectedTemplate()!)} group
              {templateGroupCount(selectedTemplate()!) !== 1 ? 's' : ''}
            </p>
            <Show when={selectedTemplate()!.description}>
              <p class="text-xcord-text-muted text-xs mt-1">{selectedTemplate()!.description}</p>
            </Show>
          </div>

          <Show when={submitError()}>
            <p class="text-red-400 text-xs" role="alert">
              {submitError()}
            </p>
          </Show>

          <div class="flex gap-2">
            <button
              class="px-4 py-2 bg-xcord-brand text-white text-sm font-medium rounded hover:bg-xcord-brand-hover transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              onClick={handleCreateFromTemplate}
              disabled={isSubmitting() || !newServerName().trim()}
              aria-label="Create Server"
            >
              {isSubmitting() ? 'Creating...' : 'Create Server'}
            </button>
            <button
              class="px-4 py-2 bg-xcord-bg-tertiary text-xcord-text-muted text-sm rounded hover:bg-xcord-bg-primary hover:text-xcord-text-primary transition-colors"
              onClick={handleCancelCreate}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      {/* Templates list */}
      <div class="flex-1 overflow-y-auto">
        <Show when={isLoading()}>
          <div class="flex items-center justify-center h-32">
            <div class="w-5 h-5 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
          </div>
        </Show>

        <Show when={!isLoading() && templates().length === 0}>
          <div class="flex flex-col items-center justify-center h-48 space-y-3">
            <p class="text-xcord-text-muted text-sm">No templates available</p>
            <Show when={props.isOwner}>
              <button
                class="text-xcord-brand hover:underline text-sm"
                onClick={() => setShowSaveForm(true)}
              >
                Save current server as a template
              </button>
            </Show>
          </div>
        </Show>

        <Show when={!isLoading() && templates().length > 0}>
          <div class="divide-y divide-xcord-bg-tertiary">
            <For each={templates()}>
              {(template) => (
                <div class="px-4 py-4 hover:bg-xcord-bg-primary/40 transition-colors">
                  <div class="flex items-start justify-between gap-3">
                    <div class="flex-1 min-w-0">
                      <h3 class="text-xcord-text-primary font-semibold text-sm">
                        {template.name}
                      </h3>
                      <Show when={template.description}>
                        <p class="text-xcord-text-muted text-xs mt-0.5 line-clamp-2">
                          {template.description}
                        </p>
                      </Show>
                      <div class="flex items-center gap-3 mt-1.5">
                        <span class="text-xcord-text-muted text-xs">
                          {templateChannelCount(template)} channel
                          {templateChannelCount(template) !== 1 ? 's' : ''}
                        </span>
                        <span class="text-xcord-text-muted text-xs">
                          {templateGroupCount(template)} group
                          {templateGroupCount(template) !== 1 ? 's' : ''}
                        </span>
                        <span class="text-xcord-text-muted text-xs">
                          Used {template.usageCount} time
                          {template.usageCount !== 1 ? 's' : ''}
                        </span>
                      </div>
                    </div>
                    <button
                      class="flex-shrink-0 px-3 py-1.5 rounded text-sm font-medium bg-xcord-bg-tertiary text-xcord-text-muted hover:bg-xcord-brand hover:text-white transition-colors"
                      onClick={() => openCreateForm(template)}
                      aria-label={`Use template ${template.name}`}
                    >
                      Use Template
                    </button>
                  </div>
                </div>
              )}
            </For>
          </div>
        </Show>
      </div>
    </div>
  );
}

export { ServerTemplates };
