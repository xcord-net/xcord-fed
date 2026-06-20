import { useModals } from '../../stores/modal.store';
import styles from './WelcomePanel.module.css';

/**
 * First-run guidance banner shown at the top of the home view when the user has
 * not created or joined any server. It points to a clear next action (create a
 * server) without hiding the friends/DM list below, which is still how a user
 * starts a direct message.
 */
export default function WelcomePanel() {
  const modals = useModals();

  return (
    <div class={styles.banner} data-testid="home-welcome">
      <div class={styles.text}>
        <p class={styles.title}>Welcome to Xcord</p>
        <p class={styles.body}>
          Create your first server to start chatting, or add a friend below to
          send a direct message.
        </p>
      </div>
      <button
        type="button"
        data-testid="welcome-create-server"
        class={styles.primary}
        onClick={() => modals.openCreateServer()}
      >
        Create a server
      </button>
    </div>
  );
}
