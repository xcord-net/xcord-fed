import { For, Show } from 'solid-js';
import { type UpgradeHistoryEntry, formatDateTime, statusClass } from './formatters';
import styles from './UpdatesTab.module.css';

interface UpgradeHistoryProps {
  entries: UpgradeHistoryEntry[] | null;
  expanded: boolean;
  onToggle: () => void;
}

export default function UpgradeHistory(props: UpgradeHistoryProps) {
  return (
    <div data-testid="updates-history-section" class={styles.historySection}>
      <button
        data-testid="updates-history-toggle"
        type="button"
        class={styles.historyToggleButton}
        onClick={props.onToggle}
        aria-expanded={props.expanded}
      >
        <span class={styles.historyToggleLabel}>Upgrade History</span>
        <span
          class={`${styles.historyToggleChevron} ${props.expanded ? styles.historyToggleChevronExpanded : ''}`}
          aria-hidden="true"
        >
          ▼
        </span>
      </button>

      <Show when={props.expanded}>
        <div data-testid="updates-history-list" class={styles.historyList}>
          <Show
            when={props.entries && props.entries.length > 0}
            fallback={
              <p class={styles.historyEmpty}>No upgrade history available.</p>
            }
          >
            <For each={props.entries!}>
              {(entry) => (
                <div
                  data-testid={`updates-history-entry-${entry.id}`}
                  class={styles.historyEntry}
                >
                  <div class={styles.historyEntryRow}>
                    <div class={styles.historyEntryLeft}>
                      <span class={statusClass(entry.status)}>
                        {entry.status}
                      </span>
                      <Show when={entry.previousVersion && entry.newVersion}>
                        <span class={styles.historyVersions}>
                          v{entry.previousVersion} <span class={styles.historyVersionArrow}>→</span> v{entry.newVersion}
                        </span>
                      </Show>
                      <Show when={!entry.previousVersion && entry.newVersion}>
                        <span class={styles.historyVersions}>v{entry.newVersion}</span>
                      </Show>
                    </div>
                    <div class={styles.historyTimestamp}>
                      <Show when={entry.completedAt} fallback={<span>{formatDateTime(entry.startedAt)}</span>}>
                        <span>{formatDateTime(entry.completedAt)}</span>
                      </Show>
                    </div>
                  </div>
                  <Show when={entry.errorMessage}>
                    <p class={styles.historyErrorMessage}>{entry.errorMessage}</p>
                  </Show>
                </div>
              )}
            </For>
          </Show>
        </div>
      </Show>
    </div>
  );
}
