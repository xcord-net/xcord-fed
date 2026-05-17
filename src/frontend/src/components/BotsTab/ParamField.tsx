import { Show } from 'solid-js';
import type { AgentParameterManifest } from './types';
import Flexbox from '../ui/Flexbox';
import styles from './ParamField.module.css';

export interface ParamFieldProps {
  param: AgentParameterManifest;
  value: string;
  onChange: (val: string) => void;
}

export function ParamField(props: ParamFieldProps) {
  return (
    <div class={styles.paramField}>
      <label class={styles.paramLabel}>
        {props.param.name}
        {props.param.required && <span class={styles.paramRequiredMark}>*</span>}
      </label>
      <Show when={props.param.description}>
        <p class={styles.paramDescription}>{props.param.description}</p>
      </Show>
      <Show when={props.param.type === 'boolean'}>
        <Flexbox as="label" align="center" gap={0.5} class={styles.paramCheckboxLabel}>
          <input
            type="checkbox"
            checked={props.value === 'true'}
            onChange={(e) => props.onChange(e.currentTarget.checked ? 'true' : 'false')}
            class={styles.paramCheckbox}
          />
          <span class={styles.paramCheckboxText}>Enabled</span>
        </Flexbox>
      </Show>
      <Show when={props.param.type === 'number'}>
        <input
          type="number"
          value={props.value}
          onInput={(e) => props.onChange(e.currentTarget.value)}
          class={styles.paramInput}
          required={props.param.required}
        />
      </Show>
      <Show when={props.param.type === 'string'}>
        <input
          type="text"
          value={props.value}
          onInput={(e) => props.onChange(e.currentTarget.value)}
          class={styles.paramInput}
          required={props.param.required}
        />
      </Show>
    </div>
  );
}

export default ParamField;
