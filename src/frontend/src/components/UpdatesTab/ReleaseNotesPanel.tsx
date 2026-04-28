import { Show } from 'solid-js';
import { type AvailableVersion, type ReleaseNotes, formatDate } from './formatters';
import ReleaseNotesContent from './ReleaseNotesContent';
import styles from './UpdatesTab.module.css';

interface ReleaseNotesPanelProps {
  selectedVersion: AvailableVersion | null;
  parsedNotes: ReleaseNotes | null;
  isCurrent: boolean;
  isUpgrading: boolean;
  upgradeError: string | null;
  upgradeSuccess: string | null;
  onUpgrade: () => void;
}

export default function ReleaseNotesPanel(props: ReleaseNotesPanelProps) {
  return (
    <div
      data-testid="updates-release-notes-panel"
      class={styles.releaseNotesPanel}
    >
      <Show when={props.selectedVersion} fallback={<p class={styles.releaseNotesPlaceholder}>Select a version to view release notes.</p>}>
        <div class={styles.releaseNotesHeader}>
          <h3 class={styles.releaseNotesTitle}>
            v{props.selectedVersion!.version}
          </h3>
          <Show when={!props.isCurrent}>
            <button
              data-testid="updates-upgrade-button"
              type="button"
              disabled={props.isUpgrading}
              onClick={props.onUpgrade}
              class={styles.upgradeButton}
            >
              {props.isUpgrading ? 'Upgrading...' : 'Update to this version'}
            </button>
          </Show>
          <Show when={props.isCurrent}>
            <span class={styles.currentlyInstalled}>Currently installed</span>
          </Show>
        </div>

        <Show when={props.selectedVersion!.isMinimumVersion && props.selectedVersion!.minimumEnforcementDate}>
          <div class={styles.minimumVersionBanner}>
            This is a minimum required version. Enforcement date: {formatDate(props.selectedVersion!.minimumEnforcementDate)}
          </div>
        </Show>

        <Show when={props.upgradeError}>
          <div
            data-testid="updates-upgrade-error"
            role="alert"
            class={styles.upgradeError}
          >
            {props.upgradeError}
          </div>
        </Show>

        <Show when={props.upgradeSuccess}>
          <div
            data-testid="updates-upgrade-success"
            role="status"
            class={styles.upgradeSuccess}
          >
            {props.upgradeSuccess}
          </div>
        </Show>

        <Show
          when={props.parsedNotes}
          fallback={
            <Show when={props.selectedVersion!.releaseNotes}>
              <p class={styles.rawReleaseNotes}>
                {props.selectedVersion!.releaseNotes}
              </p>
            </Show>
          }
        >
          <ReleaseNotesContent notes={props.parsedNotes!} />
        </Show>
      </Show>
    </div>
  );
}
