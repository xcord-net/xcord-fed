import { Show } from 'solid-js';
import BlockList from '../BlockList';
import NotificationSettings from '../NotificationSettings';
import Modal from '../ui/Modal';
import UserNotes from '../UserNotes';
import UserProfileEditor from '../UserProfileEditor';
import { useModals } from '../../stores/modal.store';
import Flexbox from '../ui/Flexbox';
import styles from './SettingsModal.module.css';

export default function SettingsModal() {
  const modals = useModals();

  return (
    <Modal data-testid="user-settings-modal" open={modals.showSettings !== null} onClose={() => modals.closeSettings()} aria-label="User Settings" size="lg">
      <div id="settings-modal-panel">
        <Flexbox class={styles.settingsTabs}>
          <button
            data-testid="settings-tab-profile"
            class={`${styles.settingsTab}${modals.showSettings === 'profile' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('profile')}
          >
            Profile
          </button>
          <button
            data-testid="settings-tab-notifications"
            class={`${styles.settingsTab}${modals.showSettings === 'notifications' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('notifications')}
          >
            Notifications
          </button>
          <button
            class={`${styles.settingsTab}${modals.showSettings === 'blocks' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('blocks')}
          >
            Blocked Users
          </button>
          <button
            class={`${styles.settingsTab}${modals.showSettings === 'notes' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('notes')}
          >
            User Notes
          </button>
        </Flexbox>
        <Show when={modals.showSettings === 'profile'}><UserProfileEditor /></Show>
        <Show when={modals.showSettings === 'notifications'}><NotificationSettings /></Show>
        <Show when={modals.showSettings === 'blocks'}><BlockList /></Show>
        <Show when={modals.showSettings === 'notes'}><UserNotes /></Show>
      </div>
    </Modal>
  );
}
