import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import UpgradeHistory from './UpgradeHistory';
import type { UpgradeHistoryEntry } from './formatters';

const entry: UpgradeHistoryEntry = {
  id: 'h-1',
  status: 'Completed',
  previousVersion: '0.1.0',
  newVersion: '0.2.0',
  targetImage: 'xcord/fed:0.2.0',
  errorMessage: null,
  startedAt: '2026-02-01T10:00:00Z',
  completedAt: '2026-02-01T10:05:00Z',
};

describe('UpgradeHistory', () => {
  it('renders without crashing with collapsed state', () => {
    const { getByTestId, queryByTestId } = render(() => (
      <UpgradeHistory entries={[entry]} expanded={false} onToggle={vi.fn()} />
    ));
    expect(getByTestId('updates-history-section')).toBeInTheDocument();
    expect(queryByTestId('updates-history-list')).toBeNull();
  });

  it('shows the toggle button with Upgrade History label', () => {
    const { getByTestId } = render(() => (
      <UpgradeHistory entries={null} expanded={false} onToggle={vi.fn()} />
    ));
    const btn = getByTestId('updates-history-toggle');
    expect(btn).toHaveTextContent('Upgrade History');
    expect(btn).toHaveAttribute('aria-expanded', 'false');
  });

  it('invokes onToggle when toggle button is clicked', () => {
    const onToggle = vi.fn();
    const { getByTestId } = render(() => (
      <UpgradeHistory entries={[entry]} expanded={false} onToggle={onToggle} />
    ));
    fireEvent.click(getByTestId('updates-history-toggle'));
    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it('renders entries when expanded with non-empty entries', () => {
    const { getByTestId, container } = render(() => (
      <UpgradeHistory entries={[entry]} expanded={true} onToggle={vi.fn()} />
    ));
    expect(getByTestId('updates-history-list')).toBeInTheDocument();
    expect(getByTestId('updates-history-entry-h-1')).toBeInTheDocument();
    expect(container.textContent).toContain('0.1.0');
    expect(container.textContent).toContain('0.2.0');
    expect(container.textContent).toContain('Completed');
  });

  it('renders empty state when expanded with no entries', () => {
    const { container } = render(() => (
      <UpgradeHistory entries={[]} expanded={true} onToggle={vi.fn()} />
    ));
    expect(container.textContent).toContain('No upgrade history available');
  });

  it('renders error message when entry has errorMessage', () => {
    const failed: UpgradeHistoryEntry = {
      ...entry,
      id: 'h-2',
      status: 'Failed',
      errorMessage: 'Container failed to start',
    };
    const { container } = render(() => (
      <UpgradeHistory entries={[failed]} expanded={true} onToggle={vi.fn()} />
    ));
    expect(container.textContent).toContain('Container failed to start');
  });
});
