import { For, Show } from 'solid-js';
import { type ReleaseNotes } from './formatters';
import styles from './UpdatesTab.module.css';

interface ReleaseNotesContentProps {
  notes: ReleaseNotes;
}

export default function ReleaseNotesContent(props: ReleaseNotesContentProps) {
  return (
    <div class={styles.releaseNotesContent}>
      <Show when={props.notes.breakingChanges?.length > 0}>
        <div data-testid="updates-breaking-changes" class={styles.releaseNotesSection}>
          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleBreaking}`}>
            Breaking Changes
          </h4>
          <ul class={styles.releaseNotesList}>
            <For each={props.notes.breakingChanges}>
              {(item) => (
                <li class={styles.releaseNotesListItem}>
                  <span class={styles.listIconBreaking}>!</span>
                  {item}
                </li>
              )}
            </For>
          </ul>
        </div>
      </Show>

      <Show when={props.notes.features?.length > 0}>
        <div data-testid="updates-features" class={styles.releaseNotesSection}>
          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleFeatures}`}>
            New Features
          </h4>
          <ul class={styles.releaseNotesList}>
            <For each={props.notes.features}>
              {(item) => (
                <li class={styles.releaseNotesListItem}>
                  <span class={styles.listIconFeature}>+</span>
                  {item.summary}
                </li>
              )}
            </For>
          </ul>
        </div>
      </Show>

      <Show when={props.notes.fixes?.length > 0}>
        <div data-testid="updates-fixes" class={styles.releaseNotesSection}>
          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleFixes}`}>
            Bug Fixes
          </h4>
          <ul class={styles.releaseNotesList}>
            <For each={props.notes.fixes}>
              {(item) => (
                <li class={styles.releaseNotesListItem}>
                  <span class={styles.listIconFix}>*</span>
                  {item.summary}
                </li>
              )}
            </For>
          </ul>
        </div>
      </Show>

      <Show when={props.notes.other?.length > 0}>
        <div data-testid="updates-other" class={styles.releaseNotesSection}>
          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleOther}`}>
            Other Changes
          </h4>
          <ul class={styles.releaseNotesList}>
            <For each={props.notes.other}>
              {(item) => (
                <li class={styles.releaseNotesListItem}>
                  <span class={styles.listIconOther}>
                    [{item.type}]
                  </span>
                  {item.summary}
                </li>
              )}
            </For>
          </ul>
        </div>
      </Show>

      <Show when={props.notes.migrationNotes}>
        <div data-testid="updates-migration-notes" class={styles.releaseNotesSection}>
          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleMigration}`}>
            Migration Notes
          </h4>
          <p class={styles.releaseNotesPreWrap}>
            {props.notes.migrationNotes}
          </p>
        </div>
      </Show>

      <Show when={props.notes.knownIssues}>
        <div data-testid="updates-known-issues" class={styles.releaseNotesSection}>
          <h4 class={`${styles.releaseNotesSectionTitle} ${styles.sectionTitleKnownIssues}`}>
            Known Issues
          </h4>
          <p class={styles.releaseNotesPreWrap}>
            {props.notes.knownIssues}
          </p>
        </div>
      </Show>

      <Show
        when={
          !props.notes.features?.length &&
          !props.notes.fixes?.length &&
          !props.notes.other?.length &&
          !props.notes.breakingChanges?.length &&
          !props.notes.migrationNotes &&
          !props.notes.knownIssues
        }
      >
        <p class={styles.noReleaseNotes}>No release notes available for this version.</p>
      </Show>
    </div>
  );
}
