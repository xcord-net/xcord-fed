import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './AutomodManager.module.css';

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

  return (
    <div class={styles.configEditorSpace}>
      <Show when={props.triggerType === 'Keyword'}>
        <div>
          <label class={styles.fieldLabel}>
            Keywords (comma-separated)
          </label>
          <input
            type="text"
            aria-label="Keywords"
            class={styles.input}
            value={getField('keywords')}
            onInput={(e) => setArrayField('keywords', e.currentTarget.value)}
            placeholder="badword, spam, etc."
          />
        </div>
        <label class={styles.checkboxLabel}>
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
          <label class={styles.fieldLabel}>Pattern</label>
          <input
            type="text"
            aria-label="Regex pattern"
            class={styles.input}
            value={getField('pattern')}
            onInput={(e) => setField('pattern', e.currentTarget.value)}
            placeholder="e.g. (buy|sell)\s+crypto"
          />
        </div>
        <label class={styles.checkboxLabel}>
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
          <label class={styles.fieldLabel}>
            Max mentions per message
          </label>
          <input
            type="number"
            aria-label="Max mentions"
            class={styles.input}
            value={getField('maxMentions')}
            min="1"
            onInput={(e) => setField('maxMentions', parseInt(e.currentTarget.value, 10) || 5)}
          />
        </div>
      </Show>

      <Show when={props.triggerType === 'MessageSpam'}>
        <div>
          <label class={styles.fieldLabel}>
            Max messages
          </label>
          <input
            type="number"
            aria-label="Max messages"
            class={styles.input}
            value={getField('maxMessages')}
            min="1"
            onInput={(e) => setField('maxMessages', parseInt(e.currentTarget.value, 10) || 5)}
          />
        </div>
        <div>
          <label class={styles.fieldLabel}>
            Interval (seconds)
          </label>
          <input
            type="number"
            aria-label="Interval seconds"
            class={styles.input}
            value={getField('intervalSeconds')}
            min="1"
            onInput={(e) => setField('intervalSeconds', parseInt(e.currentTarget.value, 10) || 10)}
          />
        </div>
      </Show>

      <Show when={props.triggerType === 'LinkFilter'}>
        <div>
          <label class={styles.fieldLabel}>
            Blocked domains (comma-separated)
          </label>
          <input
            type="text"
            aria-label="Blocked domains"
            class={styles.input}
            value={getField('blockedDomains')}
            onInput={(e) => setArrayField('blockedDomains', e.currentTarget.value)}
            placeholder="spam.com, phishing.net"
          />
        </div>
        <div>
          <label class={styles.fieldLabel}>
            Allowed domains only (comma-separated, leave empty for blocklist mode)
          </label>
          <input
            type="text"
            aria-label="Allowed domains"
            class={styles.input}
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
      setError(getErrorMessage(err, 'Failed to load automod rules'));
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
      setError(getErrorMessage(err, 'Failed to create rule'));
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
      setError(getErrorMessage(err, 'Failed to update rule'));
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
      setError(getErrorMessage(err, 'Failed to delete rule'));
    }
  }

  onMount(() => {
    loadRules();
  });

  return (
    <div class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.headerTitle}>Automod Rules</h2>
        <button
          type="button"
          aria-label="Create automod rule"
          class={styles.addButton}
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
        <div role="alert" class={styles.errorBanner}>
          {error()}
        </div>
      </Show>
      <Show when={successMsg()}>
        <div role="status" class={styles.successBanner}>
          {successMsg()}
        </div>
      </Show>

      {/* Create form */}
      <Show when={showCreate()}>
        <form
          onSubmit={handleCreate}
          aria-label="Create automod rule"
          class={styles.createForm}
        >
          <h3 class={styles.formTitle}>New Automod Rule</h3>

          <div>
            <label for="automod-rule-name" class={styles.fieldLabel}>
              Rule Name <span class={styles.requiredMark}>*</span>
            </label>
            <input
              id="automod-rule-name"
              type="text"
              required
              maxlength="100"
              class={styles.input}
              value={createName()}
              onInput={(e) => setCreateName(e.currentTarget.value)}
              placeholder="e.g. Block Profanity"
            />
          </div>

          <div class={styles.twoColGrid}>
            <div>
              <label for="automod-trigger-type" class={styles.fieldLabel}>
                Trigger Type
              </label>
              <select
                id="automod-trigger-type"
                class={`${styles.select} ${styles.selectFull}`}
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
              <label for="automod-action-type" class={styles.fieldLabel}>
                Action
              </label>
              <select
                id="automod-action-type"
                class={`${styles.select} ${styles.selectFull}`}
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

          <label class={styles.checkboxLabel}>
            <input
              type="checkbox"
              aria-label="Rule enabled"
              checked={createEnabled()}
              onChange={(e) => setCreateEnabled(e.currentTarget.checked)}
            />
            Enable rule immediately
          </label>

          <div class={styles.formActions}>
            <button
              type="button"
              class={styles.cancelButton}
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
              class={styles.submitButton}
            >
              {isCreating() ? 'Creating...' : 'Create Rule'}
            </button>
          </div>
        </form>
      </Show>

      {/* Rules list */}
      <div class={styles.ruleList}>
        <Show when={isLoading()}>
          <p class={styles.loadingText}>Loading automod rules...</p>
        </Show>

        <Show when={!isLoading() && rules().length === 0}>
          <div class={styles.emptyState}>
            <p class={styles.emptyTitle}>No automod rules</p>
            <p class={styles.emptySubtitle}>Click &ldquo;+ Add Rule&rdquo; to create your first rule.</p>
          </div>
        </Show>

        <For each={rules()}>
          {(rule) => (
            <div
              class={styles.ruleCard}
              aria-label={`Automod rule: ${rule.name}`}
            >
              <Show
                when={editingRuleId() === rule.id}
                fallback={
                  /* Rule display row */
                  <div class={styles.ruleRow}>
                    <div class={styles.ruleInfo}>
                      <div class={styles.ruleNameRow}>
                        <span class={styles.ruleName}>{rule.name}</span>
                        <span
                          class={rule.enabled ? styles.statusBadgeEnabled : styles.statusBadgeDisabled}
                        >
                          {rule.enabled ? 'Enabled' : 'Disabled'}
                        </span>
                      </div>
                      <p class={styles.ruleMeta}>
                        {TRIGGER_LABELS[rule.triggerType]} &rarr; {ACTION_LABELS[rule.actionType]}
                      </p>
                      <p class={styles.ruleConfig}>
                        {triggerConfigSummary(rule.triggerType, rule.triggerConfig)}
                      </p>
                    </div>

                    <div class={styles.ruleActions}>
                      <button
                        type="button"
                        aria-label={`Edit rule ${rule.name}`}
                        class={styles.editButton}
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
                            class={styles.deleteButton}
                            onClick={() => setConfirmDeleteId(rule.id)}
                          >
                            Delete
                          </button>
                        }
                      >
                        <span class={styles.deleteConfirmText}>Delete?</span>
                        <button
                          type="button"
                          aria-label="Cancel delete"
                          class={styles.deleteNoButton}
                          onClick={() => setConfirmDeleteId(null)}
                        >
                          No
                        </button>
                        <button
                          type="button"
                          aria-label="Confirm delete"
                          class={styles.deleteYesButton}
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
                <form onSubmit={handleSaveEdit} class={styles.editForm} aria-label={`Edit rule ${rule.name}`}>
                  <h3 class={styles.editFormTitle}>Edit Rule</h3>

                  <div>
                    <label class={styles.fieldLabel}>
                      Rule Name <span class={styles.requiredMark}>*</span>
                    </label>
                    <input
                      type="text"
                      required
                      maxlength="100"
                      class={styles.input}
                      aria-label="Edit rule name"
                      value={editName()}
                      onInput={(e) => setEditName(e.currentTarget.value)}
                    />
                  </div>

                  <div class={styles.twoColGrid}>
                    <div>
                      <label class={styles.fieldLabel}>Trigger Type</label>
                      <select
                        class={`${styles.select} ${styles.selectFull}`}
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
                      <label class={styles.fieldLabel}>Action</label>
                      <select
                        class={`${styles.select} ${styles.selectFull}`}
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

                  <label class={styles.checkboxLabel}>
                    <input
                      type="checkbox"
                      aria-label="Edit rule enabled"
                      checked={editEnabled()}
                      onChange={(e) => setEditEnabled(e.currentTarget.checked)}
                    />
                    Enable rule
                  </label>

                  <div class={styles.formActions}>
                    <button
                      type="button"
                      class={styles.cancelButton}
                      onClick={cancelEdit}
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      disabled={isSavingEdit()}
                      class={styles.submitButton}
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
