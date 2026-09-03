import { Show } from 'solid-js';
import BlockList from '../BlockList';
import DmList from '../DmList';
import FriendList from '../FriendList';
import NotificationSettings from '../NotificationSettings';
import UserNotes from '../UserNotes';
import UserProfileEditor from '../UserProfileEditor';
import { useModals } from '../../stores/modal.store';
import Flexbox from '../ui/Flexbox';
import Surface from '../ui/Surface';
import styles from './SettingsModal.module.css';

export interface SettingsModalProps {
  /**
   * Render as a Deck pane. The Deck opens account settings as a tab, so the
   * strip stays visible and the conversation underneath is not covered.
   */
  inline?: boolean;
}

export default function SettingsModal(props: SettingsModalProps = {}) {
  const modals = useModals();

  return (
    <Show when={props.inline || modals.showSettings !== null}>
    <Surface data-testid="user-settings-modal" inline={props.inline} onClose={() => modals.closeSettings()} aria-label="User Settings" size="lg">
      <div id="settings-modal-panel">
        <Flexbox class={styles.settingsTabs}>
          <button
            data-testid="settings-tab-profile"
            class={`${styles.settingsTab}${modals.showSettings === 'profile' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('profile')}
          >
            Profile
          </button>
          {/* People: friendships and the conversations they lead to. The Deck
              has no sidebar to hang a standing friends panel off, and Home is
              the catch-up surface rather than an address book, so relationship
              management sits here with the other account-level lists - blocks
              and notes are already the same kind of thing. */}
          <button
            data-testid="settings-tab-people"
            class={`${styles.settingsTab}${modals.showSettings === 'people' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('people')}
          >
            People
          </button>
          <button
            data-testid="settings-tab-notifications"
            class={`${styles.settingsTab}${modals.showSettings === 'notifications' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('notifications')}
          >
            Notifications
          </button>
          <button
            data-testid="settings-tab-blocks"
            class={`${styles.settingsTab}${modals.showSettings === 'blocks' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('blocks')}
          >
            Blocked Users
          </button>
          <button
            data-testid="settings-tab-notes"
            class={`${styles.settingsTab}${modals.showSettings === 'notes' ? ` ${styles.settingsTabActive}` : ''}`}
            onClick={() => modals.openSettings('notes')}
          >
            User Notes
          </button>
        </Flexbox>
        <Show when={modals.showSettings === 'profile'}><UserProfileEditor /></Show>
        <Show when={modals.showSettings === 'people'}>
          <FriendList />
          <DmList />
        </Show>
        <Show when={modals.showSettings === 'notifications'}><NotificationSettings /></Show>
        <Show when={modals.showSettings === 'blocks'}><BlockList /></Show>
        <Show when={modals.showSettings === 'notes'}><UserNotes /></Show>
      </div>
    </Surface>
    </Show>
  );
}
