import { For, Show } from 'solid-js';
import Flexbox from '../ui/Flexbox';
import styles from './RuleItem.module.css';
import TriggerConfigEditor from './TriggerConfigEditor';
import {
  ACTION_LABELS,
  ACTION_TYPES,
  TRIGGER_LABELS,
  TRIGGER_TYPES,
  defaultTriggerConfig,
  triggerConfigSummary,
  type ActionType,
  type AutomodRule,
  type TriggerType,
} from './helpers';

interface RuleItemProps {
  rule: AutomodRule;
  isEditing: boolean;
  isSavingEdit: boolean;
  confirmDelete: boolean;
  editName: string;
  editTriggerType: TriggerType;
  editTriggerConfig: string;
  editActionType: ActionType;
  editEnabled: boolean;
  onStartEdit: (rule: AutomodRule) => void;
  onCancelEdit: () => void;
  onSaveEdit: (e: Event) => void;
  onEditNameChange: (value: string) => void;
  onEditTriggerTypeChange: (value: TriggerType) => void;
  onEditTriggerConfigChange: (value: string) => void;
  onEditActionTypeChange: (value: ActionType) => void;
  onEditEnabledChange: (value: boolean) => void;
  onRequestDelete: (id: string) => void;
  onCancelDelete: () => void;
  onConfirmDelete: (id: string) => void;
}

export default function RuleItem(props: RuleItemProps) {
  return (
    <div
      class={styles.ruleCard}
      aria-label={`Automod rule: ${props.rule.name}`}
    >
      <Show
        when={props.isEditing}
        fallback={
          <Flexbox align="start" justify="between" gap={0.5} class={styles.ruleRow}>
            <div class={styles.ruleInfo}>
              <Flexbox align="center" gap={0.5} class={styles.ruleNameRow}>
                <span class={styles.ruleName}>{props.rule.name}</span>
                <span
                  class={
                    props.rule.enabled
                      ? styles.statusBadgeEnabled
                      : styles.statusBadgeDisabled
                  }
                >
                  {props.rule.enabled ? 'Enabled' : 'Disabled'}
                </span>
              </Flexbox>
              <p class={styles.ruleMeta}>
                {TRIGGER_LABELS[props.rule.triggerType]} &rarr;{' '}
                {ACTION_LABELS[props.rule.actionType]}
              </p>
              <p class={styles.ruleConfig}>
                {triggerConfigSummary(props.rule.triggerType, props.rule.triggerConfig)}
              </p>
            </div>

            <Flexbox align="center" gap={0.375} class={styles.ruleActions}>
              <button
                type="button"
                aria-label={`Edit rule ${props.rule.name}`}
                class={styles.editButton}
                onClick={() => props.onStartEdit(props.rule)}
              >
                Edit
              </button>

              <Show
                when={props.confirmDelete}
                fallback={
                  <button
                    type="button"
                    aria-label={`Delete rule ${props.rule.name}`}
                    class={styles.deleteButton}
                    onClick={() => props.onRequestDelete(props.rule.id)}
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
                  onClick={props.onCancelDelete}
                >
                  No
                </button>
                <button
                  type="button"
                  aria-label="Confirm delete"
                  class={styles.deleteYesButton}
                  onClick={() => props.onConfirmDelete(props.rule.id)}
                >
                  Yes
                </button>
              </Show>
            </Flexbox>
          </Flexbox>
        }
      >
        <form
          onSubmit={props.onSaveEdit}
          class={styles.editForm}
          aria-label={`Edit rule ${props.rule.name}`}
        >
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
              value={props.editName}
              onInput={(e) => props.onEditNameChange(e.currentTarget.value)}
            />
          </div>

          <div class={styles.twoColGrid}>
            <div>
              <label class={styles.fieldLabel}>Trigger Type</label>
              <select
                class={`${styles.select} ${styles.selectFull}`}
                aria-label="Edit trigger type"
                value={props.editTriggerType}
                onChange={(e) => {
                  const t = e.currentTarget.value as TriggerType;
                  props.onEditTriggerTypeChange(t);
                  props.onEditTriggerConfigChange(defaultTriggerConfig(t));
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
                value={props.editActionType}
                onChange={(e) =>
                  props.onEditActionTypeChange(e.currentTarget.value as ActionType)
                }
              >
                <For each={ACTION_TYPES}>
                  {(a) => <option value={a}>{ACTION_LABELS[a]}</option>}
                </For>
              </select>
            </div>
          </div>

          <TriggerConfigEditor
            triggerType={props.editTriggerType}
            triggerConfig={props.editTriggerConfig}
            onUpdate={props.onEditTriggerConfigChange}
          />

          <Flexbox as="label" align="center" gap={0.5} class={styles.checkboxLabel}>
            <input
              type="checkbox"
              aria-label="Edit rule enabled"
              checked={props.editEnabled}
              onChange={(e) => props.onEditEnabledChange(e.currentTarget.checked)}
            />
            Enable rule
          </Flexbox>

          <Flexbox gap={0.5} justify="end" class={styles.formActions}>
            <button
              type="button"
              class={styles.cancelButton}
              onClick={props.onCancelEdit}
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={props.isSavingEdit}
              class={styles.submitButton}
            >
              {props.isSavingEdit ? 'Saving...' : 'Save Changes'}
            </button>
          </Flexbox>
        </form>
      </Show>
    </div>
  );
}
