import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import VersionList from './VersionList';
import type { AvailableVersion } from './formatters';

const v1: AvailableVersion = {
  id: 'id-1',
  version: '0.1.0',
  image: 'xcord/fed:0.1.0',
  releaseNotes: null,
  isMinimumVersion: false,
  minimumEnforcementDate: null,
  publishedAt: '2026-01-01T00:00:00Z',
};

const v2: AvailableVersion = {
  id: 'id-2',
  version: '0.2.0',
  image: 'xcord/fed:0.2.0',
  releaseNotes: null,
  isMinimumVersion: true,
  minimumEnforcementDate: '2026-06-01T00:00:00Z',
  publishedAt: '2026-02-01T00:00:00Z',
};

describe('VersionList', () => {
  it('renders without crashing with empty list', () => {
    const { getByTestId } = render(() => (
      <VersionList versions={[]} selectedId={null} currentVersion="0.1.0" onSelect={vi.fn()} />
    ));
    expect(getByTestId('updates-version-list')).toBeInTheDocument();
  });

  it('renders an item for each version', () => {
    const { getByTestId } = render(() => (
      <VersionList versions={[v1, v2]} selectedId={null} currentVersion="0.1.0" onSelect={vi.fn()} />
    ));
    expect(getByTestId('updates-version-item-0.1.0')).toBeInTheDocument();
    expect(getByTestId('updates-version-item-0.2.0')).toBeInTheDocument();
  });

  it('shows the current badge for the matching current version', () => {
    const { getByTestId, queryByTestId } = render(() => (
      <VersionList versions={[v1, v2]} selectedId={null} currentVersion="0.1.0" onSelect={vi.fn()} />
    ));
    expect(getByTestId('updates-current-badge-0.1.0')).toBeInTheDocument();
    expect(queryByTestId('updates-current-badge-0.2.0')).toBeNull();
  });

  it('shows the minimum badge for minimum-version entries', () => {
    const { getByTestId, queryByTestId } = render(() => (
      <VersionList versions={[v1, v2]} selectedId={null} currentVersion="0.1.0" onSelect={vi.fn()} />
    ));
    expect(getByTestId('updates-minimum-badge-0.2.0')).toBeInTheDocument();
    expect(queryByTestId('updates-minimum-badge-0.1.0')).toBeNull();
  });

  it('invokes onSelect with the clicked version id', () => {
    const onSelect = vi.fn();
    const { getByTestId } = render(() => (
      <VersionList versions={[v1, v2]} selectedId={null} currentVersion="0.1.0" onSelect={onSelect} />
    ));
    fireEvent.click(getByTestId('updates-version-item-0.2.0'));
    expect(onSelect).toHaveBeenCalledWith('id-2');
  });
});
