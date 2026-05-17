import { For, Show } from 'solid-js';
import { hasRole, toggleRole } from './GroupManager';
import Flexbox from '../ui/Flexbox';
import styles from './GroupEditForm.module.css';
import { GROUP_COLOR_PALETTE } from '../../constants/colors';

const ROLE_FLAGS: { label: string; bit: number }[] = [
  { label: 'View Channels', bit: 1 << 0 },
  { label: 'Manage Server', bit: 1 << 1 },
  { label: 'Manage Groups', bit: 1 << 2 },
  { label: 'Manage Channels', bit: 1 << 3 },
  { label: 'Kick Members', bit: 1 << 4 },
  { label: 'Ban Members', bit: 1 << 5 },
  { label: 'Create Invites', bit: 1 << 6 },
  { label: 'Manage Messages', bit: 1 << 7 },
  { label: 'Send Messages', bit: 1 << 8 },
  { label: 'Embed Links', bit: 1 << 9 },
  { label: 'Attach Files', bit: 1 << 10 },
  { label: 'Read Message History', bit: 1 << 11 },
  { label: 'Use External Emojis', bit: 1 << 12 },
  { label: 'Connect to Voice', bit: 1 << 13 },
  { label: 'Speak in Voice', bit: 1 << 14 },
  { label: 'Mute Members', bit: 1 << 15 },
  { label: 'Deafen Members', bit: 1 << 16 },
  { label: 'Move Members', bit: 1 << 17 },
  { label: 'Video', bit: 1 << 18 },
  { label: 'Share Screen', bit: 1 << 19 },
  { label: 'Send Messages in Threads', bit: 1 << 20 },
  { label: 'Create Public Threads', bit: 1 << 21 },
  { label: 'Create Private Threads', bit: 1 << 22 },
  { label: 'Add Reactions', bit: 1 << 23 },
  { label: 'Mention Everyone', bit: 1 << 24 },
  { label: 'Change Nickname', bit: 1 << 25 },
  { label: 'Manage Nicknames', bit: 1 << 26 },
  { label: 'Timeout Members', bit: 1 << 27 },
  { label: 'Manage Emojis', bit: 1 << 28 },
  { label: 'Manage Stickers', bit: 1 << 29 },
  { label: 'Manage Webhooks', bit: 1 << 30 },
];

const PRESET_COLORS = GROUP_COLOR_PALETTE;

interface GroupEditFormProps {
  editName: string;
  editColor: string;
  editRoles: number;
  isSaving: boolean;
  saveSuccess: string;
  saveError: string;
  onNameInput: (value: string) => void;
  onColorChange: (value: string) => void;
  onRolesChange: (value: number) => void;
  onDeleteClick: () => void;
  onSubmit: (e: Event) => void;
}

export default function GroupEditForm(props: GroupEditFormProps) {
  return (
    <form onSubmit={props.onSubmit} class={styles.editForm}>
      <Flexbox align="center" justify="between" class={styles.editFormHeader}>
        <h3 class={styles.editFormTitle}>Edit Group</h3>

        {/* Delete button */}
        <button
          type="button"
          data-testid="delete-group-button"
          onClick={props.onDeleteClick}
          class={styles.deleteGroupButton}
        >
          Delete Group
        </button>
      </Flexbox>

      {/* Group name */}
      <div class={styles.fieldGroup}>
        <label for="edit-group-name" class={styles.fieldLabel}>
          Group Name
        </label>
        <input
          id="edit-group-name"
          type="text"
          required
          maxlength="100"
          value={props.editName}
          onInput={(e) => props.onNameInput(e.currentTarget.value)}
          class={styles.textInput}
        />
      </div>

      {/* Color picker */}
      <div class={styles.fieldGroup}>
        <label class={styles.fieldLabel}>
          Group Color
        </label>

        {/* Preset color swatches */}
        <Flexbox wrap="wrap" gap={0.5} class={styles.colorSwatches} role="group" aria-label="Preset colors">
          <For each={PRESET_COLORS}>
            {(color) => (
              <button
                type="button"
                aria-label={`Color ${color}`}
                aria-pressed={props.editColor === color}
                onClick={() => props.onColorChange(color)}
                class={`${styles.colorSwatch} ${props.editColor === color ? styles.colorSwatchSelected : ''}`}
                style={{ 'background-color': color }}
              />
            )}
          </For>
        </Flexbox>

        {/* Hex input */}
        <Flexbox align="center" gap={0.5} class={styles.hexRow}>
          <span
            class={styles.colorPreview}
            style={{ 'background-color': props.editColor }}
            aria-hidden="true"
          />
          <label for="color-hex" class="sr-only">Hex color</label>
          <input
            id="color-hex"
            type="text"
            maxlength="7"
            value={props.editColor}
            onInput={(e) => {
              const val = e.currentTarget.value;
              if (/^#[0-9a-fA-F]{0,6}$/.test(val)) props.onColorChange(val);
            }}
            aria-label="Hex color value"
            class={styles.hexInput}
          />
        </Flexbox>
      </div>

      {/* Roles (permission flags) */}
      <div class={styles.fieldGroup}>
        <label class={styles.fieldLabel}>
          Roles
        </label>
        <div class={styles.rolesList}>
          <For each={ROLE_FLAGS}>
            {(flag) => (
              <Flexbox as="label" align="center" gap={0.75} data-testid={`permission-${flag.label.toLowerCase().replace(/\s+/g, '-')}`} class={styles.roleItem}>
                <input
                  type="checkbox"
                  checked={hasRole(props.editRoles, flag.bit)}
                  onChange={() => props.onRolesChange(toggleRole(props.editRoles, flag.bit))}
                  class={styles.roleCheckbox}
                />
                <span class={styles.roleLabel}>
                  {flag.label}
                </span>
              </Flexbox>
            )}
          </For>
        </div>
      </div>

      {/* Status messages */}
      <Show when={props.saveSuccess}>
        <div role="status" class={styles.successMsg}>
          {props.saveSuccess}
        </div>
      </Show>
      <Show when={props.saveError}>
        <div role="alert" class={styles.errorMsgInline}>
          {props.saveError}
        </div>
      </Show>

      {/* Save button */}
      <Flexbox justify="end" class={styles.saveRow}>
        <button
          data-testid="group-save-changes-button"
          type="submit"
          disabled={props.isSaving}
          class={styles.saveButton}
        >
          {props.isSaving ? 'Saving...' : 'Save Changes'}
        </button>
      </Flexbox>
    </form>
  );
}
