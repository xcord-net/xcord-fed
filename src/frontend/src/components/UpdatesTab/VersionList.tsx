import { For, Show } from 'solid-js';
import { type AvailableVersion, formatDate } from './formatters';
import styles from './UpdatesTab.module.css';

interface VersionListProps {
  versions: AvailableVersion[];
  selectedId: string | null;
  currentVersion: string;
  onSelect: (id: string) => void;
}

export default function VersionList(props: VersionListProps) {
  return (
    <div
      data-testid="updates-version-list"
      class={styles.versionList}
    >
      <div class={styles.versionListHeader}>
        <p class={styles.versionListHeaderText}>
          Available Versions
        </p>
      </div>
      <For each={props.versions}>
        {(v) => (
          <button
            data-testid={`updates-version-item-${v.version}`}
            type="button"
            class={`${styles.versionItem} ${props.selectedId === v.id ? styles.versionItemSelected : styles.versionItemDefault}`}
            onClick={() => props.onSelect(v.id)}
          >
            <div class={styles.versionItemRow}>
              <span class={styles.versionMono}>v{v.version}</span>
              <Show when={v.version === props.currentVersion}>
                <span
                  data-testid={`updates-current-badge-${v.version}`}
                  class={styles.badgeCurrent}
                >
                  current
                </span>
              </Show>
              <Show when={v.isMinimumVersion}>
                <span
                  data-testid={`updates-minimum-badge-${v.version}`}
                  class={styles.badgeMinimum}
                >
                  min
                </span>
              </Show>
            </div>
            <p class={styles.versionItemDate}>{formatDate(v.publishedAt)}</p>
          </button>
        )}
      </For>
    </div>
  );
}
