import { Show } from 'solid-js';
import { useAuth } from '../../stores/auth.store';
import { useModals } from '../../stores/modal.store';
import { tooltip } from '../../directives/tooltip';
import { GearIcon, PinIcon, SearchIcon, ThreadsIcon } from './icons';
import Flexbox from '../ui/Flexbox';
import styles from './ChannelHeader.module.css';

// Ensure the directive is not tree-shaken
void tooltip;

interface ChannelHeaderProps {
  channelName: string | undefined;
  channelTopic: string | null | undefined;
}

export default function ChannelHeader(props: ChannelHeaderProps) {
  const authStore = useAuth();
  const modals = useModals();

  return (
    <Flexbox align="center" class={styles.channelHeader}>
      <h2 class={styles.channelName}>{props.channelName}</h2>
      <Show when={props.channelTopic}>
        <span class={styles.channelTopic}>
          {props.channelTopic}
        </span>
      </Show>

      {/* Header action buttons */}
      <Flexbox align="center" gap={0.25} class={styles.headerActions}>
        <button
          data-testid="pins-button"
          title="Pinned Messages"
          aria-label="Pinned Messages"
          use:tooltip="Pinned Messages"
          class={`${styles.headerBtn}${modals.showPins ? ` ${styles.headerBtnActive}` : ''}`}
          onClick={() => modals.togglePins()}
        >
          <PinIcon />
        </button>
        <button
          data-testid="threads-button"
          title="Threads"
          aria-label="Threads"
          use:tooltip="Threads"
          class={`${styles.headerBtn}${modals.showThreads ? ` ${styles.headerBtnActive}` : ''}`}
          onClick={() => modals.toggleThreads()}
        >
          <ThreadsIcon />
        </button>
        <button
          data-testid="search-button"
          title="Search"
          aria-label="Search"
          use:tooltip="Search"
          class={`${styles.headerBtn}${modals.showSearch ? ` ${styles.headerBtnActive}` : ''}`}
          onClick={() => modals.toggleSearch()}
        >
          <SearchIcon />
        </button>
        <button
          data-testid="channel-settings-button"
          title="Channel Settings"
          aria-label="Channel Settings"
          use:tooltip="Channel Settings"
          class={`${styles.headerBtn}${modals.showChannelSettings ? ` ${styles.headerBtnActive}` : ''}`}
          onClick={() => modals.toggleChannelSettings()}
        >
          <GearIcon />
        </button>
        <Show when={authStore.user?.isAdmin}>
          <button
            data-testid="server-settings-button"
            title="Server Settings"
            aria-label="Server Settings"
            use:tooltip="Server Settings"
            class={`${styles.headerBtn}${modals.showServerSettings ? ` ${styles.headerBtnActive}` : ''}`}
            onClick={() => modals.openServerSettings()}
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class={styles.headerIcon}>
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
            </svg>
          </button>
        </Show>
      </Flexbox>
    </Flexbox>
  );
}
