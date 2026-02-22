import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';

// -- Types ------------------------------------------------------------------

type TriggerType = 'Keyword' | 'Regex' | 'MentionSpam' | 'MessageSpam' | 'LinkFilter';
type ActionType = 'DeleteMessage' | 'TimeoutUser' | 'AlertMods' | 'BlockMessage';

interface AutomodRule {
  id: string;
  serverId: string;
  name: string;
  enabled: boolean;
  triggerType: TriggerType;
  triggerConfig: string;
  actionType: ActionType;
  actionConfig: string | null;
  exemptRoleIds: string | null;
  exemptChannelIds: string | null;
  exemptBots: boolean;
  createdAt: string;
}

interface AutomodManagerProps {
  serverId: string;
}

const TRIGGER_LABELS: Record<TriggerType, string> = {
  Keyword: 'Keyword Filter',
  Regex: 'Regex Pattern',
  MentionSpam: 'Mention Spam',
  MessageSpam: 'Message Spam',
  LinkFilter: 'Link Filter',
};

const ACTION_LABELS: Record<ActionType, string> = {
  DeleteMessage: 'Delete Message',
  TimeoutUser: 'Timeout User',
  AlertMods: 'Alert Moderators',
  BlockMessage: 'Block Message',
};

const TRIGGER_TYPES: TriggerType[] = ['Keyword', 'Regex', 'MentionSpam', 'MessageSpam', 'LinkFilter'];
const ACTION_TYPES: ActionType[] = ['BlockMessage', 'DeleteMessage', 'TimeoutUser', 'AlertMods'];

// -- Default trigger config helpers -----------------------------------------

function defaultTriggerConfig(type: TriggerType): string {
  switch (type) {
    case 'Keyword':
      return JSON.stringify({ keywords: [], matchWholeWord: false });
    case 'Regex':
      return JSON.stringify({ pattern: '', caseSensitive: false });
    case 'MentionSpam':
      return JSON.stringify({ maxMentions: 5 });
    case 'MessageSpam':
      return JSON.stringify({ maxMessages: 5, intervalSeconds: 10 });
    case 'LinkFilter':
      return JSON.stringify({ blockedDomains: [], allowedDomains: [] });
  }
}

// -- Trigger config display --------------------------------------------------

function triggerConfigSummary(type: TriggerType, config: string): string {
  try {
    const parsed = JSON.parse(config);
    switch (type) {
      case 'Keyword':
        return (parsed.keywords as string[])?.join(', ') || '(no keywords)';
      case 'Regex':
        return parsed.pattern || '(no pattern)';
      case 'MentionSpam':
        return `max ${parsed.maxMentions} mentions`;
      case 'MessageSpam':
        return `max ${parsed.maxMessages} msgs / ${parsed.intervalSeconds}s`;
      case 'LinkFilter': {
        const blocked: string[] = parsed.blockedDomains ?? [];
        const allowed: string[] = parsed.allowedDomains ?? [];
        if (blocked.length > 0) return `blocked: ${blocked.join(', ')}`;
        if (allowed.length > 0) return `allowed: ${allowed.join(', ')}`;
        return '(no domains configured)';
      }
    }
  } catch {
    return config;
  }
}

// -- Trigger Config Editor --------------------------------------------------

interface TriggerConfigEditorProps {
  triggerType: TriggerType;
  triggerConfig: string;
  onUpdate: (config: string) => void;
}

function TriggerConfigEditor(props: TriggerConfigEditorProps) {
  function getField(field: string): string {
    try {
      const parsed = JSON.parse(props.triggerConfig);
      const v = parsed[field];
      if (Array.isArray(v)) return v.join(', ');
      return String(v ?? '');
    } catch {
      return '';
    }
  }

  function getBoolField(field: string): boolean {
    try {
      const parsed = JSON.parse(props.triggerConfig);
      return Boolean(parsed[field]);
    } catch {
      return false;
    }
  }

  function setField(field: string, value: unknown) {
    try {
      const parsed = JSON.parse(props.triggerConfig);
      parsed[field] = value;
      props.onUpdate(JSON.stringify(parsed));
    } catch {
      // Keep existing config on parse error
    }
  }

  function setArrayField(field: string, csv: string) {
    const arr = csv
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean);
    setField(field, arr);
  }

  const inputClass =
    'w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-1.5 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand';

  return (
    <div class="space-y-2">
      <Show when={props.triggerType === 'Keyword'}>
        <div>
          <label class="block text-xs text-xcord-text-muted mb-1">
            Keywords (comma-separated)
          </label>
          <input
            type="text"
            aria-label="Keywords"
            class={inputClass}
            value={getField('keywords')}
            onInput={(e) => setArrayField('keywords', e.currentTarget.value)}
            placeholder="badword, spam, etc."
          />
        </div>
        <label class="flex items-center gap-2 text-sm text-xcord-text-primary cursor-pointer">
          <input
            type="checkbox"
            aria-label="Match whole word only"
            checked={getBoolField('matchWholeWord')}
            onChange={(e) => setField('matchWholeWord', e.currentTarget.checked)}
          />
          Match whole word only
        </label>
      </Show>

      <Show when={props.triggerType === 'Regex'}>
        <div>
          <label class="block text-xs text-xcord-text-muted mb-1">Pattern</label>
          <input
            type="text"
            aria-label="Regex pattern"
            class={inputClass}
            value={getField('pattern')}
            onInput={(e) => setField('pattern', e.currentTarget.value)}
            placeholder="e.g. (buy|sell)\s+crypto"
          />
        </div>
        <label class="flex items-center gap-2 text-sm text-xcord-text-primary cursor-pointer">
          <input
            type="checkbox"
            aria-label="Case sensitive"
            checked={getBoolField('caseSensitive')}
            onChange={(e) => setField('caseSensitive', e.currentTarget.checked)}
          />
          Case sensitive
        </label>
      </Show>

      <Show when={props.triggerType === 'MentionSpam'}>
        <div>
          <label class="block text-xs text-xcord-text-muted mb-1">
            Max mentions per message
          </label>
          <input
            type="number"
            aria-label="Max mentions"
            class={inputClass}
            value={getField('maxMentions')}
            min="1"
            onInput={(e) => setField('maxMentions', parseInt(e.currentTarget.value, 10) || 5)}
          />
        </div>
      </Show>

      <Show when={props.triggerType === 'MessageSpam'}>
        <div>
          <label class="block text-xs text-xcord-text-muted mb-1">
            Max messages
          </label>
          <input
            type="number"
            aria-label="Max messages"
            class={inputClass}
            value={getField('maxMessages')}
            min="1"
            onInput={(e) => setField('maxMessages', parseInt(e.currentTarget.value, 10) || 5)}
          />
        </div>
        <div>
          <label class="block text-xs text-xcord-text-muted mb-1">
            Interval (seconds)
          </label>
          <input
            type="number"
            aria-label="Interval seconds"
            class={inputClass}
            value={getField('intervalSeconds')}
            min="1"
            onInput={(e) => setField('intervalSeconds', parseInt(e.currentTarget.value, 10) || 10)}
          />
        </div>
      </Show>

      <Show when={props.triggerType === 'LinkFilter'}>
        <div>
          <label class="block text-xs text-xcord-text-muted mb-1">
            Blocked domains (comma-separated)
          </label>
          <input
            type="text"
            aria-label="Blocked domains"
            class={inputClass}
            value={getField('blockedDomains')}
            onInput={(e) => setArrayField('blockedDomains', e.currentTarget.value)}
            placeholder="spam.com, phishing.net"
          />
        </div>
        <div>
          <label class="block text-xs text-xcord-text-muted mb-1">
            Allowed domains only (comma-separated, leave empty for blocklist mode)
          </label>
          <input
            type="text"
            aria-label="Allowed domains"
            class={inputClass}
            value={getField('allowedDomains')}
            onInput={(e) => setArrayField('allowedDomains', e.currentTarget.value)}
            placeholder="example.com, trusted.org"
          />
        </div>
      </Show>
    </div>
  );
}

// -- Main component ---------------------------------------------------------

export default function AutomodManager(props: AutomodManagerProps) {
  const [rules, setRules] = createSignal<AutomodRule[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [successMsg, setSuccessMsg] = createSignal<string | null>(null);

  // Create form
  const [showCreate, setShowCreate] = createSignal(false);
  const [createName, setCreateName] = createSignal('');
  const [createTriggerType, setCreateTriggerType] = createSignal<TriggerType>('Keyword');
  const [createTriggerConfig, setCreateTriggerConfig] = createSignal(
    defaultTriggerConfig('Keyword')
  );
  const [createActionType, setCreateActionType] = createSignal<ActionType>('BlockMessage');
  const [createEnabled, setCreateEnabled] = createSignal(true);
  const [isCreating, setIsCreating] = createSignal(false);

  // Edit form
  const [editingRuleId, setEditingRuleId] = createSignal<string | null>(null);
  const [editName, setEditName] = createSignal('');
  const [editTriggerType, setEditTriggerType] = createSignal<TriggerType>('Keyword');
  const [editTriggerConfig, setEditTriggerConfig] = createSignal('');
  const [editActionType, setEditActionType] = createSignal<ActionType>('BlockMessage');
  const [editEnabled, setEditEnabled] = createSignal(true);
  const [isSavingEdit, setIsSavingEdit] = createSignal(false);

  // Delete confirm
  const [confirmDeleteId, setConfirmDeleteId] = createSignal<string | null>(null);

  function showSuccess(msg: string) {
    setSuccessMsg(msg);
    setError(null);
    setTimeout(() => setSuccessMsg(null), 3_000);
  }

  async function loadRules() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.get<{ rules: AutomodRule[] }>(
        `/api/v1/servers/${props.serverId}/automod-rules`
      );
      setRules(result.rules ?? []);
    } catch (err: unknown) {
      const e = err as { error?: string; detail?: string };
      setError(e?.detail ?? e?.error ?? 'Failed to load automod rules');
    } finally {
      setIsLoading(false);
    }
  }

  async function handleCreate(e: Event) {
    e.preventDefault();
    if (!createName().trim()) return;
    setIsCreating(true);
    setError(null);
    try {
      const created = await api.post<AutomodRule>(
        `/api/v1/servers/${props.serverId}/automod-rules`,
        {
          name: createName().trim(),
          enabled: createEnabled(),
          triggerType: createTriggerType(),
          triggerConfig: createTriggerConfig(),
          actionType: createActionType(),
          actionConfig: null,
          exemptRoleIds: null,
          exemptChannelIds: null,
          exemptBots: false,
        }
      );
      setRules([...rules(), created]);
      setShowCreate(false);
      setCreateName('');
      setCreateTriggerType('Keyword');
      setCreateTriggerConfig(defaultTriggerConfig('Keyword'));
      setCreateActionType('BlockMessage');
      setCreateEnabled(true);
      showSuccess('Rule created successfully.');
    } catch (err: unknown) {
      const e = err as { error?: string; detail?: string };
      setError(e?.detail ?? e?.error ?? 'Failed to create rule');
    } finally {
      setIsCreating(false);
    }
  }

  function startEdit(rule: AutomodRule) {
    setEditingRuleId(rule.id);
    setEditName(rule.name);
    setEditTriggerType(rule.triggerType);
    setEditTriggerConfig(rule.triggerConfig);
    setEditActionType(rule.actionType);
    setEditEnabled(rule.enabled);
  }

  function cancelEdit() {
    setEditingRuleId(null);
  }

  async function handleSaveEdit(e: Event) {
    e.preventDefault();
    const ruleId = editingRuleId();
    if (!ruleId) return;
    setIsSavingEdit(true);
    setError(null);
    try {
      const updated = await api.patch<AutomodRule>(
        `/api/v1/servers/${props.serverId}/automod-rules/${ruleId}`,
        {
          name: editName().trim(),
          enabled: editEnabled(),
          triggerType: editTriggerType(),
          triggerConfig: editTriggerConfig(),
          actionType: editActionType(),
        }
      );
      setRules(rules().map((r) => (r.id === ruleId ? updated : r)));
      setEditingRuleId(null);
      showSuccess('Rule updated successfully.');
    } catch (err: unknown) {
      const e = err as { error?: string; detail?: string };
      setError(e?.detail ?? e?.error ?? 'Failed to update rule');
    } finally {
      setIsSavingEdit(false);
    }
  }

  async function handleDelete(ruleId: string) {
    setError(null);
    try {
      await api.delete(`/api/v1/servers/${props.serverId}/automod-rules/${ruleId}`);
      setRules(rules().filter((r) => r.id !== ruleId));
      setConfirmDeleteId(null);
      showSuccess('Rule deleted.');
    } catch (err: unknown) {
      const e = err as { error?: string; detail?: string };
      setError(e?.detail ?? e?.error ?? 'Failed to delete rule');
    }
  }

  onMount(() => {
    loadRules();
  });

  const selectClass =
    'bg-xcord-bg-primary text-xcord-text-primary rounded px-2 py-1.5 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand';

  const inputClass =
    'w-full bg-xcord-bg-primary text-xcord-text-primary rounded px-3 py-1.5 text-sm border border-xcord-border focus:border-xcord-brand focus:outline-none focus-visible:ring-2 focus-visible:ring-xcord-brand';

  return (
    <div class="flex flex-col h-full">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-border flex items-center justify-between">
        <h2 class="text-white font-semibold">Automod Rules</h2>
        <button
          type="button"
          aria-label="Create automod rule"
          class="px-3 py-1.5 bg-xcord-brand hover:bg-xcord-brand-hover text-white text-sm font-medium rounded transition-colors focus-visible:ring-2 focus-visible:ring-xcord-brand focus-visible:outline-none"
          onClick={() => {
            setShowCreate(true);
            setError(null);
          }}
        >
          + Add Rule
        </button>
      </div>

      {/* Status messages */}
      <Show when={error()}>
        <div role="alert" class="mx-4 mt-3 px-4 py-2 bg-red-500/20 border border-red-500/30 rounded text-red-400 text-sm">
          {error()}
        </div>
      </Show>
      <Show when={successMsg()}>
        <div role="status" class="mx-4 mt-3 px-4 py-2 bg-green-500/20 border border-green-500/30 rounded text-green-400 text-sm">
          {successMsg()}
        </div>
      </Show>

      {/* Create form */}
      <Show when={showCreate()}>
        <form
          onSubmit={handleCreate}
          aria-label="Create automod rule"
          class="mx-4 mt-3 mb-1 p-4 bg-xcord-bg-tertiary rounded border border-xcord-border space-y-3"
        >
          <h3 class="text-white font-medium text-sm">New Automod Rule</h3>

          <div>
            <label for="automod-rule-name" class="block text-xs text-xcord-text-muted mb-1">
              Rule Name <span class="text-red-400">*</span>
            </label>
            <input
              id="automod-rule-name"
              type="text"
              required
              maxlength="100"
              class={inputClass}
              value={createName()}
              onInput={(e) => setCreateName(e.currentTarget.value)}
              placeholder="e.g. Block Profanity"
            />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div>
              <label for="automod-trigger-type" class="block text-xs text-xcord-text-muted mb-1">
                Trigger Type
              </label>
              <select
                id="automod-trigger-type"
                class={selectClass + ' w-full'}
                value={createTriggerType()}
                onChange={(e) => {
                  const t = e.currentTarget.value as TriggerType;
                  setCreateTriggerType(t);
                  setCreateTriggerConfig(defaultTriggerConfig(t));
                }}
              >
                <For each={TRIGGER_TYPES}>
                  {(t) => <option value={t}>{TRIGGER_LABELS[t]}</option>}
                </For>
              </select>
            </div>

            <div>
              <label for="automod-action-type" class="block text-xs text-xcord-text-muted mb-1">
                Action
              </label>
              <select
                id="automod-action-type"
                class={selectClass + ' w-full'}
                value={createActionType()}
                onChange={(e) => setCreateActionType(e.currentTarget.value as ActionType)}
              >
                <For each={ACTION_TYPES}>
                  {(a) => <option value={a}>{ACTION_LABELS[a]}</option>}
                </For>
              </select>
            </div>
          </div>

          {/* Trigger config editor */}
          <TriggerConfigEditor
            triggerType={createTriggerType()}
            triggerConfig={createTriggerConfig()}
            onUpdate={setCreateTriggerConfig}
          />

          <label class="flex items-center gap-2 text-sm text-xcord-text-primary cursor-pointer">
            <input
              type="checkbox"
              aria-label="Rule enabled"
              checked={createEnabled()}
              onChange={(e) => setCreateEnabled(e.currentTarget.checked)}
            />
            Enable rule immediately
          </label>

          <div class="flex gap-2 justify-end">
            <button
              type="button"
              class="px-3 py-1.5 text-sm text-xcord-text-muted hover:text-white rounded transition-colors"
              onClick={() => {
                setShowCreate(false);
                setError(null);
              }}
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isCreating()}
              class="px-4 py-1.5 bg-xcord-brand hover:bg-xcord-brand-hover text-white text-sm font-medium rounded transition-colors disabled:opacity-50"
            >
              {isCreating() ? 'Creating...' : 'Create Rule'}
            </button>
          </div>
        </form>
      </Show>

      {/* Rules list */}
      <div class="flex-1 overflow-y-auto px-4 py-3 space-y-2">
        <Show when={isLoading()}>
          <p class="text-xcord-text-muted text-sm">Loading automod rules...</p>
        </Show>

        <Show when={!isLoading() && rules().length === 0}>
          <div class="text-center py-8 text-xcord-text-muted">
            <p class="font-semibold">No automod rules</p>
            <p class="text-sm mt-1">Click &ldquo;+ Add Rule&rdquo; to create your first rule.</p>
          </div>
        </Show>

        <For each={rules()}>
          {(rule) => (
            <div
              class="bg-xcord-bg-tertiary rounded border border-xcord-border p-3"
              aria-label={`Automod rule: ${rule.name}`}
            >
              <Show
                when={editingRuleId() === rule.id}
                fallback={
                  /* Rule display row */
                  <div class="flex items-start justify-between gap-2">
                    <div class="flex-1 min-w-0">
                      <div class="flex items-center gap-2">
                        <span class="text-white font-medium text-sm">{rule.name}</span>
                        <span
                          class={`text-xs px-1.5 py-0.5 rounded ${
                            rule.enabled
                              ? 'bg-green-500/20 text-green-400'
                              : 'bg-xcord-bg-primary text-xcord-text-muted'
                          }`}
                        >
                          {rule.enabled ? 'Enabled' : 'Disabled'}
                        </span>
                      </div>
                      <p class="text-xs text-xcord-text-muted mt-0.5">
                        {TRIGGER_LABELS[rule.triggerType]} &rarr; {ACTION_LABELS[rule.actionType]}
                      </p>
                      <p class="text-xs text-xcord-text-muted truncate">
                        {triggerConfigSummary(rule.triggerType, rule.triggerConfig)}
                      </p>
                    </div>

                    <div class="flex items-center gap-1.5 flex-shrink-0">
                      <button
                        type="button"
                        aria-label={`Edit rule ${rule.name}`}
                        class="px-2.5 py-1 text-xs bg-xcord-bg-primary hover:bg-xcord-bg-secondary text-xcord-text-muted hover:text-white rounded transition-colors"
                        onClick={() => startEdit(rule)}
                      >
                        Edit
                      </button>

                      <Show
                        when={confirmDeleteId() === rule.id}
                        fallback={
                          <button
                            type="button"
                            aria-label={`Delete rule ${rule.name}`}
                            class="px-2.5 py-1 text-xs bg-red-500/20 hover:bg-red-500/40 text-red-400 rounded transition-colors"
                            onClick={() => setConfirmDeleteId(rule.id)}
                          >
                            Delete
                          </button>
                        }
                      >
                        <span class="text-xs text-xcord-text-muted">Delete?</span>
                        <button
                          type="button"
                          aria-label="Cancel delete"
                          class="px-2 py-1 text-xs bg-xcord-bg-primary text-xcord-text-muted rounded"
                          onClick={() => setConfirmDeleteId(null)}
                        >
                          No
                        </button>
                        <button
                          type="button"
                          aria-label="Confirm delete"
                          class="px-2 py-1 text-xs bg-red-600 hover:bg-red-700 text-white rounded transition-colors"
                          onClick={() => handleDelete(rule.id)}
                        >
                          Yes
                        </button>
                      </Show>
                    </div>
                  </div>
                }
              >
                {/* Edit form (inline) */}
                <form onSubmit={handleSaveEdit} class="space-y-3" aria-label={`Edit rule ${rule.name}`}>
                  <h3 class="text-white font-medium text-sm">Edit Rule</h3>

                  <div>
                    <label class="block text-xs text-xcord-text-muted mb-1">
                      Rule Name <span class="text-red-400">*</span>
                    </label>
                    <input
                      type="text"
                      required
                      maxlength="100"
                      class={inputClass}
                      aria-label="Edit rule name"
                      value={editName()}
                      onInput={(e) => setEditName(e.currentTarget.value)}
                    />
                  </div>

                  <div class="grid grid-cols-2 gap-3">
                    <div>
                      <label class="block text-xs text-xcord-text-muted mb-1">Trigger Type</label>
                      <select
                        class={selectClass + ' w-full'}
                        aria-label="Edit trigger type"
                        value={editTriggerType()}
                        onChange={(e) => {
                          const t = e.currentTarget.value as TriggerType;
                          setEditTriggerType(t);
                          setEditTriggerConfig(defaultTriggerConfig(t));
                        }}
                      >
                        <For each={TRIGGER_TYPES}>
                          {(t) => <option value={t}>{TRIGGER_LABELS[t]}</option>}
                        </For>
                      </select>
                    </div>
                    <div>
                      <label class="block text-xs text-xcord-text-muted mb-1">Action</label>
                      <select
                        class={selectClass + ' w-full'}
                        aria-label="Edit action type"
                        value={editActionType()}
                        onChange={(e) => setEditActionType(e.currentTarget.value as ActionType)}
                      >
                        <For each={ACTION_TYPES}>
                          {(a) => <option value={a}>{ACTION_LABELS[a]}</option>}
                        </For>
                      </select>
                    </div>
                  </div>

                  <TriggerConfigEditor
                    triggerType={editTriggerType()}
                    triggerConfig={editTriggerConfig()}
                    onUpdate={setEditTriggerConfig}
                  />

                  <label class="flex items-center gap-2 text-sm text-xcord-text-primary cursor-pointer">
                    <input
                      type="checkbox"
                      aria-label="Edit rule enabled"
                      checked={editEnabled()}
                      onChange={(e) => setEditEnabled(e.currentTarget.checked)}
                    />
                    Enable rule
                  </label>

                  <div class="flex gap-2 justify-end">
                    <button
                      type="button"
                      class="px-3 py-1.5 text-sm text-xcord-text-muted hover:text-white rounded transition-colors"
                      onClick={cancelEdit}
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      disabled={isSavingEdit()}
                      class="px-4 py-1.5 bg-xcord-brand hover:bg-xcord-brand-hover text-white text-sm font-medium rounded transition-colors disabled:opacity-50"
                    >
                      {isSavingEdit() ? 'Saving...' : 'Save Changes'}
                    </button>
                  </div>
                </form>
              </Show>
            </div>
          )}
        </For>
      </div>
    </div>
  );
}
