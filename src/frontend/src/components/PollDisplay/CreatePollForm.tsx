import { For, Show, createSignal } from 'solid-js';
import styles from './CreatePollForm.module.css';

interface PollOption_Draft {
  id: string;
  text: string;
}

interface CreatePollFormProps {
  onSubmit: (pollData: {
    question: string;
    options: string[];
    allowMultiSelect: boolean;
    durationHours?: number;
  }) => void;
  onCancel: () => void;
}

export default function CreatePollForm(props: CreatePollFormProps) {
  const [question, setQuestion] = createSignal('');
  const [options, setOptions] = createSignal<PollOption_Draft[]>([
    { id: '1', text: '' },
    { id: '2', text: '' },
  ]);
  const [allowMultiSelect, setAllowMultiSelect] = createSignal(false);
  const [durationHours, setDurationHours] = createSignal<number | undefined>(undefined);
  const [validationError, setValidationError] = createSignal<string | null>(null);

  let nextId = 3;

  const addOption = () => {
    if (options().length >= 10) return;
    setOptions([...options(), { id: String(nextId++), text: '' }]);
  };

  const removeOption = (id: string) => {
    if (options().length <= 2) return;
    setOptions(options().filter((o) => o.id !== id));
  };

  const updateOption = (id: string, text: string) => {
    setOptions(options().map((o) => (o.id === id ? { ...o, text } : o)));
  };

  const handleSubmit = () => {
    const q = question().trim();
    if (!q) {
      setValidationError('Please enter a question.');
      return;
    }
    const filledOptions = options().map((o) => o.text.trim()).filter(Boolean);
    if (filledOptions.length < 2) {
      setValidationError('Please provide at least 2 options.');
      return;
    }
    setValidationError(null);
    props.onSubmit({
      question: q,
      options: filledOptions,
      allowMultiSelect: allowMultiSelect(),
      durationHours: durationHours(),
    });
  };

  return (
    <div class={styles.createPollForm}>
      <h4 class={styles.createPollTitle}>Create Poll</h4>

      {/* Question */}
      <div class={styles.fieldGroup}>
        <label class={styles.fieldLabel}>
          Question
        </label>
        <input
          data-testid="poll-question-input"
          type="text"
          class={styles.textInput}
          placeholder="Ask a question..."
          value={question()}
          onInput={(e) => setQuestion(e.currentTarget.value)}
        />
      </div>

      {/* Options */}
      <div class={styles.fieldGroup}>
        <label class={styles.fieldLabel}>
          Options ({options().length}/10)
        </label>
        <div class={styles.optionInputList}>
          <For each={options()}>
            {(opt, index) => (
              <div class={styles.optionInputRow}>
                <input
                  data-testid={`poll-option-input-${index()}`}
                  type="text"
                  class={styles.optionTextInput}
                  placeholder={`Option ${index() + 1}`}
                  value={opt.text}
                  onInput={(e) => updateOption(opt.id, e.currentTarget.value)}
                />
                <Show when={options().length > 2}>
                  <button
                    class={styles.removeOptionButton}
                    onClick={() => removeOption(opt.id)}
                    aria-label={`Remove option ${index() + 1}`}
                  >
                    ✕
                  </button>
                </Show>
              </div>
            )}
          </For>
        </div>
        <Show when={options().length < 10}>
          <button
            data-testid="poll-add-option-button"
            class={styles.addOptionButton}
            onClick={addOption}
          >
            + Add option
          </button>
        </Show>
      </div>

      {/* Settings row */}
      <div class={styles.settingsRow}>
        {/* Multi-select toggle */}
        <label class={styles.checkboxLabel}>
          <input
            type="checkbox"
            class={styles.checkboxInput}
            checked={allowMultiSelect()}
            onChange={(e) => setAllowMultiSelect(e.currentTarget.checked)}
          />
          <span class={styles.checkboxText}>Allow multiple selections</span>
        </label>

        {/* Duration */}
        <div class={styles.durationGroup}>
          <label class={styles.durationLabel}>Duration</label>
          <select
            class={styles.durationSelect}
            value={durationHours() ?? ''}
            onChange={(e) => {
              const v = e.currentTarget.value;
              setDurationHours(v === '' ? undefined : Number(v));
            }}
          >
            <option value="">No limit</option>
            <option value="1">1 hour</option>
            <option value="6">6 hours</option>
            <option value="12">12 hours</option>
            <option value="24">24 hours</option>
            <option value="72">3 days</option>
            <option value="168">1 week</option>
          </select>
        </div>
      </div>

      <Show when={validationError()}>
        <p class={styles.validationError}>{validationError()}</p>
      </Show>

      <div class={styles.formActions}>
        <button
          data-testid="poll-submit-button"
          class={styles.submitButton}
          onClick={handleSubmit}
        >
          Add Poll
        </button>
        <button
          data-testid="poll-cancel-button"
          class={styles.cancelButton}
          onClick={props.onCancel}
        >
          Cancel
        </button>
      </div>
    </div>
  );
}
