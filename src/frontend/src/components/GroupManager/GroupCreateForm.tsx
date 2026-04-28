import styles from './GroupCreateForm.module.css';

interface GroupCreateFormProps {
  newGroupName: string;
  isCreating: boolean;
  onNameInput: (value: string) => void;
  onSubmit: (e: Event) => void;
  onCancel: () => void;
}

export default function GroupCreateForm(props: GroupCreateFormProps) {
  return (
    <form onSubmit={props.onSubmit} class={styles.createForm}>
      <h3 class={styles.formHeading}>Create New Group</h3>
      <div class={styles.fieldGroup}>
        <label for="new-group-name" class={styles.fieldLabel}>
          Group Name <span class={styles.required}>*</span>
        </label>
        <input
          id="new-group-name"
          type="text"
          required
          maxlength="100"
          value={props.newGroupName}
          onInput={(e) => props.onNameInput(e.currentTarget.value)}
          class={styles.textInput}
          placeholder="New Group"
        />
      </div>
      <div class={styles.formActions}>
        <button
          type="submit"
          data-testid="create-group-submit-button"
          disabled={props.isCreating}
          class={styles.submitButton}
        >
          {props.isCreating ? 'Creating...' : 'Create'}
        </button>
        <button
          type="button"
          onClick={props.onCancel}
          class={styles.cancelFormButton}
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
