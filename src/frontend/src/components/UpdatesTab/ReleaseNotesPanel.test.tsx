import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import ReleaseNotesPanel from './ReleaseNotesPanel';
import type { AvailableVersion, ReleaseNotes } from './formatters';

const baseVersion: AvailableVersion = {
  id: 'id-1',
  version: '0.2.0',
  image: 'xcord/fed:0.2.0',
  releaseNotes: null,
  isMinimumVersion: false,
  minimumEnforcementDate: null,
  publishedAt: '2026-02-01T00:00:00Z',
};

const emptyNotes: ReleaseNotes = {
  version: '0.2.0',
  features: [],
  fixes: [],
  other: [],
  breakingChanges: [],
  migrationNotes: '',
  knownIssues: '',
};

describe('ReleaseNotesPanel', () => {
  it('renders placeholder when no version is selected', () => {
    const { getByTestId, container } = render(() => (
      <ReleaseNotesPanel
        selectedVersion={null}
        parsedNotes={null}
        isCurrent={false}
        isUpgrading={false}
        upgradeError={null}
        upgradeSuccess={null}
        onUpgrade={vi.fn()}
      />
    ));
    expect(getByTestId('updates-release-notes-panel')).toBeInTheDocument();
    expect(container.textContent).toContain('Select a version to view release notes');
  });

  it('renders the upgrade button when selected version is not current', () => {
    const { getByTestId } = render(() => (
      <ReleaseNotesPanel
        selectedVersion={baseVersion}
        parsedNotes={emptyNotes}
        isCurrent={false}
        isUpgrading={false}
        upgradeError={null}
        upgradeSuccess={null}
        onUpgrade={vi.fn()}
      />
    ));
    expect(getByTestId('updates-upgrade-button')).toHaveTextContent('Update to this version');
  });

  it('shows Currently installed when isCurrent is true', () => {
    const { container, queryByTestId } = render(() => (
      <ReleaseNotesPanel
        selectedVersion={baseVersion}
        parsedNotes={emptyNotes}
        isCurrent={true}
        isUpgrading={false}
        upgradeError={null}
        upgradeSuccess={null}
        onUpgrade={vi.fn()}
      />
    ));
    expect(container.textContent).toContain('Currently installed');
    expect(queryByTestId('updates-upgrade-button')).toBeNull();
  });

  it('invokes onUpgrade when the upgrade button is clicked', () => {
    const onUpgrade = vi.fn();
    const { getByTestId } = render(() => (
      <ReleaseNotesPanel
        selectedVersion={baseVersion}
        parsedNotes={emptyNotes}
        isCurrent={false}
        isUpgrading={false}
        upgradeError={null}
        upgradeSuccess={null}
        onUpgrade={onUpgrade}
      />
    ));
    fireEvent.click(getByTestId('updates-upgrade-button'));
    expect(onUpgrade).toHaveBeenCalledTimes(1);
  });

  it('shows Upgrading... and disables button while isUpgrading is true', () => {
    const { getByTestId } = render(() => (
      <ReleaseNotesPanel
        selectedVersion={baseVersion}
        parsedNotes={emptyNotes}
        isCurrent={false}
        isUpgrading={true}
        upgradeError={null}
        upgradeSuccess={null}
        onUpgrade={vi.fn()}
      />
    ));
    const btn = getByTestId('updates-upgrade-button') as HTMLButtonElement;
    expect(btn).toBeDisabled();
    expect(btn).toHaveTextContent('Upgrading...');
  });

  it('renders upgrade error and success banners when provided', () => {
    const { getByTestId } = render(() => (
      <ReleaseNotesPanel
        selectedVersion={baseVersion}
        parsedNotes={emptyNotes}
        isCurrent={false}
        isUpgrading={false}
        upgradeError="Upgrade failed"
        upgradeSuccess="Upgrade started"
        onUpgrade={vi.fn()}
      />
    ));
    expect(getByTestId('updates-upgrade-error')).toHaveTextContent('Upgrade failed');
    expect(getByTestId('updates-upgrade-success')).toHaveTextContent('Upgrade started');
  });
});
