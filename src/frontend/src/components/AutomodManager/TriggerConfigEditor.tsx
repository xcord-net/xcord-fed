import { Show } from 'solid-js';
import Flexbox from '../ui/Flexbox';
import styles from './TriggerConfigEditor.module.css';
import type { TriggerType } from './helpers';

interface TriggerConfigEditorProps {
  triggerType: TriggerType;
  triggerConfig: string;
  onUpdate: (config: string) => void;
}

export default function TriggerConfigEditor(props: TriggerConfigEditorProps) {
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
        <Flexbox as="label" align="center" gap={0.5} class={styles.checkboxLabel}>
          <input
            type="checkbox"
            aria-label="Match whole word only"
            checked={getBoolField('matchWholeWord')}
            onChange={(e) => setField('matchWholeWord', e.currentTarget.checked)}
          />
          Match whole word only
        </Flexbox>
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
        <Flexbox as="label" align="center" gap={0.5} class={styles.checkboxLabel}>
          <input
            type="checkbox"
            aria-label="Case sensitive"
            checked={getBoolField('caseSensitive')}
            onChange={(e) => setField('caseSensitive', e.currentTarget.checked)}
          />
          Case sensitive
        </Flexbox>
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
