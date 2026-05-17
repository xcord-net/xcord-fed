import Modal from '../ui/Modal';
import Flexbox from '../ui/Flexbox';
import { Capability, hasCapability } from '../../types/channel';
import sharedStyles from './Sidebar.module.css';
import styles from './CreateChannelModal.module.css';

export interface CreateChannelModalProps {
  open: boolean;
  name: string;
  capabilities: number;
  onNameInput: (value: string) => void;
  onToggleCapability: (cap: number) => void;
  onClose: () => void;
  onSubmit: () => void;
}

/** Modal for creating a new channel. State lives in the parent so reset
 *  semantics on cancel/close match the original implementation exactly. */
export default function CreateChannelModal(props: CreateChannelModalProps) {
  return (
    <Modal
      open={props.open}
      onClose={() => props.onClose()}
      title="Create Channel"
      size="sm"
    >
      <Flexbox direction="vertical" gap={1} class={styles.createModalBody}>
        <Flexbox direction="vertical" gap={0.375}>
          <label for="new-channel-name" class={styles.createModalLabel}>Channel Name</label>
          <input
            id="new-channel-name"
            data-testid="create-channel-name-input"
            type="text"
            placeholder="new-channel"
            value={props.name}
            onInput={(e) => props.onNameInput(e.currentTarget.value)}
            class={styles.createModalInput}
            onKeyPress={(e) => { if (e.key === 'Enter') props.onSubmit(); }}
          />
        </Flexbox>
        <Flexbox direction="vertical" gap={0.375}>
          <span class={styles.createModalLabel}>Capabilities</span>
          <Flexbox direction="vertical" gap={0.5}>
            <label class={styles.createModalCheckbox}>
              <input data-testid="capability-checkbox-chat" type="checkbox" checked={hasCapability(props.capabilities, Capability.Chat)} onChange={() => props.onToggleCapability(Capability.Chat)} />
              Chat
            </label>
            <label class={styles.createModalCheckbox}>
              <input data-testid="capability-checkbox-voice" type="checkbox" checked={hasCapability(props.capabilities, Capability.Voice)} onChange={() => props.onToggleCapability(Capability.Voice)} />
              Voice
            </label>
            <label class={styles.createModalCheckbox}>
              <input data-testid="capability-checkbox-video" type="checkbox" checked={hasCapability(props.capabilities, Capability.Video)} onChange={() => props.onToggleCapability(Capability.Video)} />
              Video
            </label>
            <label class={styles.createModalCheckbox}>
              <input data-testid="capability-checkbox-forum" type="checkbox" checked={hasCapability(props.capabilities, Capability.Forum)} onChange={() => props.onToggleCapability(Capability.Forum)} />
              Forum
            </label>
            <label class={styles.createModalCheckbox}>
              <input data-testid="capability-checkbox-announcement" type="checkbox" checked={hasCapability(props.capabilities, Capability.Announcement)} onChange={() => props.onToggleCapability(Capability.Announcement)} />
              Announcement
            </label>
            <label class={styles.createModalCheckbox}>
              <input data-testid="capability-checkbox-streaming" type="checkbox" checked={hasCapability(props.capabilities, Capability.Streaming)} onChange={() => props.onToggleCapability(Capability.Streaming)} />
              Streaming
            </label>
          </Flexbox>
        </Flexbox>
        <Flexbox justify="end" gap={0.75} class={styles.createModalActions}>
          <button
            data-testid="create-channel-cancel-button"
            type="button"
            class={sharedStyles.cancelButton}
            onClick={() => props.onClose()}
          >
            Cancel
          </button>
          <button
            data-testid="create-channel-submit-button"
            type="button"
            class={styles.createModalSubmit}
            onClick={() => props.onSubmit()}
          >
            Create Channel
          </button>
        </Flexbox>
      </Flexbox>
    </Modal>
  );
}
