import { For, Show, createSignal, createEffect } from 'solid-js';
import { api } from '../api/client';
import styles from './ServerTemplates.module.css';
import EmptyState from './ui/EmptyState';
import { LayoutTemplate } from 'lucide-solid';

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
    <div class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Server Templates</h2>
        <Show when={props.isOwner}>
          <button
            class={styles.saveBtn}
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
        <div class={styles.successBanner}>
          {successMessage()}
        </div>
      </Show>

      {/* Save Template Form */}
      <Show when={showSaveForm()}>
        <div class={styles.formPanel}>
          <h3 class={styles.formTitle}>Save Server as Template</h3>

          <div>
            <label class={styles.fieldLabel}>
              Template Name *
            </label>
            <input
              type="text"
              class={styles.textInput}
              placeholder="Template name..."
              value={formName()}
              onInput={(e) => setFormName(e.currentTarget.value)}
              aria-label="Template Name"
            />
          </div>

          <div>
            <label class={styles.fieldLabel}>
              Description
            </label>
            <textarea
              class={styles.textarea}
              placeholder="Describe this template..."
              rows={3}
              value={formDescription()}
              onInput={(e) => setFormDescription(e.currentTarget.value)}
              aria-label="Template Description"
            />
          </div>

          <Show when={submitError()}>
            <p class={styles.errorText} role="alert">
              {submitError()}
            </p>
          </Show>

          <div class={styles.formButtons}>
            <button
              class={styles.primaryBtn}
              onClick={handleSaveTemplate}
              disabled={isSubmitting()}
              aria-label="Save Template"
            >
              {isSubmitting() ? 'Saving...' : 'Save Template'}
            </button>
            <button
              class={styles.secondaryBtn}
              onClick={handleCancelSave}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      {/* Create from Template Form */}
      <Show when={showCreateForm() && selectedTemplate()}>
        <div class={styles.formPanel}>
          <h3 class={styles.formTitle}>
            Create Server from "{selectedTemplate()!.name}"
          </h3>

          <div>
            <label class={styles.fieldLabel}>
              New Server Name *
            </label>
            <input
              type="text"
              class={styles.textInput}
              placeholder="My new server..."
              value={newServerName()}
              onInput={(e) => setNewServerName(e.currentTarget.value)}
              aria-label="New Server Name"
            />
          </div>

          {/* Template preview */}
          <div class={styles.templatePreview}>
            <p class={styles.previewLabel}>
              Template Preview
            </p>
            <p class={styles.previewStat}>
              {templateChannelCount(selectedTemplate()!)} channel
              {templateChannelCount(selectedTemplate()!) !== 1 ? 's' : ''}
            </p>
            <p class={styles.previewStat}>
              {templateGroupCount(selectedTemplate()!)} group
              {templateGroupCount(selectedTemplate()!) !== 1 ? 's' : ''}
            </p>
            <Show when={selectedTemplate()!.description}>
              <p class={styles.previewDesc}>{selectedTemplate()!.description}</p>
            </Show>
          </div>

          <Show when={submitError()}>
            <p class={styles.errorText} role="alert">
              {submitError()}
            </p>
          </Show>

          <div class={styles.formButtons}>
            <button
              class={styles.primaryBtn}
              onClick={handleCreateFromTemplate}
              disabled={isSubmitting() || !newServerName().trim()}
              aria-label="Create Server"
            >
              {isSubmitting() ? 'Creating...' : 'Create Server'}
            </button>
            <button
              class={styles.secondaryBtn}
              onClick={handleCancelCreate}
            >
              Cancel
            </button>
          </div>
        </div>
      </Show>

      {/* Templates list */}
      <div class={styles.listArea}>
        <Show when={isLoading()}>
          <div class={styles.loadingCenter}>
            <div class={styles.spinner} />
          </div>
        </Show>

        <Show when={!isLoading() && templates().length === 0}>
          <EmptyState
            icon={LayoutTemplate}
            title="No templates yet"
            body="A template captures this community's channels and roles so you can start another like it."
            action={props.isOwner
              ? { label: 'Save this community as a template', onClick: () => setShowSaveForm(true) }
              : undefined}
            data-testid="server-templates-empty"
          />
        </Show>

        <Show when={!isLoading() && templates().length > 0}>
          <div class={styles.divideList}>
            <For each={templates()}>
              {(template) => (
                <div class={styles.templateItem}>
                  <div class={styles.templateItemInner}>
                    <div class={styles.templateInfo}>
                      <h3 class={styles.templateName}>
                        {template.name}
                      </h3>
                      <Show when={template.description}>
                        <p class={styles.templateDescription}>
                          {template.description}
                        </p>
                      </Show>
                      <div class={styles.templateMeta}>
                        <span class={styles.templateMetaText}>
                          {templateChannelCount(template)} channel
                          {templateChannelCount(template) !== 1 ? 's' : ''}
                        </span>
                        <span class={styles.templateMetaText}>
                          {templateGroupCount(template)} group
                          {templateGroupCount(template) !== 1 ? 's' : ''}
                        </span>
                        <span class={styles.templateMetaText}>
                          Used {template.usageCount} time
                          {template.usageCount !== 1 ? 's' : ''}
                        </span>
                      </div>
                    </div>
                    <button
                      class={styles.useTemplateBtn}
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
