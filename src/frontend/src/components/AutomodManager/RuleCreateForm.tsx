import { For } from 'solid-js';
import Flexbox from '../ui/Flexbox';
import styles from './RuleCreateForm.module.css';
import TriggerConfigEditor from './TriggerConfigEditor';
import {
  ACTION_LABELS,
  ACTION_TYPES,
  TRIGGER_LABELS,
  TRIGGER_TYPES,
  defaultTriggerConfig,
  type ActionType,
  type TriggerType,
} from './helpers';

interface RuleCreateFormProps {
  name: string;
  triggerType: TriggerType;
  triggerConfig: string;
  actionType: ActionType;
  enabled: boolean;
  isCreating: boolean;
  onNameChange: (value: string) => void;
  onTriggerTypeChange: (value: TriggerType) => void;
  onTriggerConfigChange: (value: string) => void;
  onActionTypeChange: (value: ActionType) => void;
  onEnabledChange: (value: boolean) => void;
  onSubmit: (e: Event) => void;
  onCancel: () => void;
}

export default function RuleCreateForm(props: RuleCreateFormProps) {
  return (
    <form
      onSubmit={props.onSubmit}
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
          value={props.name}
          onInput={(e) => props.onNameChange(e.currentTarget.value)}
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
            value={props.triggerType}
            onChange={(e) => {
              const t = e.currentTarget.value as TriggerType;
              props.onTriggerTypeChange(t);
              props.onTriggerConfigChange(defaultTriggerConfig(t));
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
            value={props.actionType}
            onChange={(e) => props.onActionTypeChange(e.currentTarget.value as ActionType)}
          >
            <For each={ACTION_TYPES}>
              {(a) => <option value={a}>{ACTION_LABELS[a]}</option>}
            </For>
          </select>
        </div>
      </div>

      <TriggerConfigEditor
        triggerType={props.triggerType}
        triggerConfig={props.triggerConfig}
        onUpdate={props.onTriggerConfigChange}
      />

      <label class={styles.checkboxLabel}>
        <input
          type="checkbox"
          aria-label="Rule enabled"
          checked={props.enabled}
          onChange={(e) => props.onEnabledChange(e.currentTarget.checked)}
        />
        Enable rule immediately
      </label>

      <Flexbox gap={0.5} justify="end">
        <button
          type="button"
          class={styles.cancelButton}
          onClick={props.onCancel}
        >
          Cancel
        </button>
        <button
          data-testid="automod-create-rule-submit"
          type="submit"
          disabled={props.isCreating}
          class={styles.submitButton}
        >
          {props.isCreating ? 'Creating...' : 'Create Rule'}
        </button>
      </Flexbox>
    </form>
  );
}
