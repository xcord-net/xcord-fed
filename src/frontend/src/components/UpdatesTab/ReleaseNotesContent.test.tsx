import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import ReleaseNotesContent from './ReleaseNotesContent';
import type { ReleaseNotes } from './formatters';

const empty: ReleaseNotes = {
  version: '0.1.0',
  features: [],
  fixes: [],
  other: [],
  breakingChanges: [],
  migrationNotes: '',
  knownIssues: '',
};

describe('ReleaseNotesContent', () => {
  it('renders without crashing on empty notes', () => {
    const { container } = render(() => <ReleaseNotesContent notes={empty} />);
    expect(container.textContent).toContain('No release notes available');
  });

  it('renders new features section when features are present', () => {
    const notes: ReleaseNotes = {
      ...empty,
      features: [{ summary: 'Added dark mode', commit: 'abc123' }],
    };
    const { getByTestId, container } = render(() => <ReleaseNotesContent notes={notes} />);
    expect(getByTestId('updates-features')).toBeInTheDocument();
    expect(container.textContent).toContain('Added dark mode');
  });

  it('renders breaking changes section when breakingChanges are present', () => {
    const notes: ReleaseNotes = {
      ...empty,
      breakingChanges: ['Removed legacy endpoint'],
    };
    const { getByTestId, container } = render(() => <ReleaseNotesContent notes={notes} />);
    expect(getByTestId('updates-breaking-changes')).toBeInTheDocument();
    expect(container.textContent).toContain('Removed legacy endpoint');
  });

  it('renders bug fixes section when fixes are present', () => {
    const notes: ReleaseNotes = {
      ...empty,
      fixes: [{ summary: 'Fixed crash on startup', commit: 'def456' }],
    };
    const { getByTestId, container } = render(() => <ReleaseNotesContent notes={notes} />);
    expect(getByTestId('updates-fixes')).toBeInTheDocument();
    expect(container.textContent).toContain('Fixed crash on startup');
  });

  it('renders migration notes when provided', () => {
    const notes: ReleaseNotes = {
      ...empty,
      migrationNotes: 'Run database migration before upgrading.',
    };
    const { getByTestId, container } = render(() => <ReleaseNotesContent notes={notes} />);
    expect(getByTestId('updates-migration-notes')).toBeInTheDocument();
    expect(container.textContent).toContain('Run database migration');
  });

  it('renders known issues when provided', () => {
    const notes: ReleaseNotes = {
      ...empty,
      knownIssues: 'Dark theme has minor issues.',
    };
    const { getByTestId, container } = render(() => <ReleaseNotesContent notes={notes} />);
    expect(getByTestId('updates-known-issues')).toBeInTheDocument();
    expect(container.textContent).toContain('Dark theme has minor issues');
  });
});
