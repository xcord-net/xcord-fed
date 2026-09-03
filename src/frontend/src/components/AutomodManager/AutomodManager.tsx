import { createSignal, For, Show, onMount } from 'solid-js';
import { api } from '../../api/client';
import { getErrorMessage } from '../../utils/errors';
import styles from './AutomodManager.module.css';
import Flexbox from '../ui/Flexbox';
import RuleCreateForm from './RuleCreateForm';
import RuleItem from './RuleItem';
import {
  defaultTriggerConfig,
  type ActionType,
  type AutomodRule,
  type TriggerType,
} from './helpers';
import EmptyState from '../ui/EmptyState';
import { ShieldCheck } from 'lucide-solid';

interface AutomodManagerProps {
  serverId: string;
}

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
      <Flexbox align="center" justify="between" class={styles.header}>
        <h2 data-testid="automod-heading" class={styles.headerTitle}>Automod Rules</h2>
        <button
          data-testid="automod-add-rule-button"
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
      </Flexbox>

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

      <Show when={showCreate()}>
        <RuleCreateForm
          name={createName()}
          triggerType={createTriggerType()}
          triggerConfig={createTriggerConfig()}
          actionType={createActionType()}
          enabled={createEnabled()}
          isCreating={isCreating()}
          onNameChange={setCreateName}
          onTriggerTypeChange={setCreateTriggerType}
          onTriggerConfigChange={setCreateTriggerConfig}
          onActionTypeChange={setCreateActionType}
          onEnabledChange={setCreateEnabled}
          onSubmit={handleCreate}
          onCancel={() => {
            setShowCreate(false);
            setError(null);
          }}
        />
      </Show>

      <div class={styles.ruleList}>
        <Show when={isLoading()}>
          <p class={styles.loadingText}>Loading automod rules...</p>
        </Show>

        <Show when={!isLoading() && rules().length === 0}>
          <EmptyState
            icon={ShieldCheck}
            title="No automod rules yet"
            body="Rules watch new messages and act on the ones that match."
            data-testid="automod-empty"
          />
        </Show>

        <For each={rules()}>
          {(rule) => (
            <RuleItem
              rule={rule}
              isEditing={editingRuleId() === rule.id}
              isSavingEdit={isSavingEdit()}
              confirmDelete={confirmDeleteId() === rule.id}
              editName={editName()}
              editTriggerType={editTriggerType()}
              editTriggerConfig={editTriggerConfig()}
              editActionType={editActionType()}
              editEnabled={editEnabled()}
              onStartEdit={startEdit}
              onCancelEdit={cancelEdit}
              onSaveEdit={handleSaveEdit}
              onEditNameChange={setEditName}
              onEditTriggerTypeChange={setEditTriggerType}
              onEditTriggerConfigChange={setEditTriggerConfig}
              onEditActionTypeChange={setEditActionType}
              onEditEnabledChange={setEditEnabled}
              onRequestDelete={setConfirmDeleteId}
              onCancelDelete={() => setConfirmDeleteId(null)}
              onConfirmDelete={handleDelete}
            />
          )}
        </For>
      </div>
    </div>
  );
}
