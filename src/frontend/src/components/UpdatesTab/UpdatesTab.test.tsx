import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import UpdatesTab from './UpdatesTab';
import { mockFetch } from '../../tests/helpers/mockFetch';
import type { SystemVersionResponse } from './formatters';

const baseResponse: SystemVersionResponse = {
  currentVersion: '0.1.0',
  hubConnected: true,
  batchUpgradesEnabled: false,
  availableVersions: [
    {
      id: 'id-1',
      version: '0.1.0',
      image: 'xcord/fed:0.1.0',
      releaseNotes: null,
      isMinimumVersion: false,
      minimumEnforcementDate: null,
      publishedAt: '2026-01-01T00:00:00Z',
    },
    {
      id: 'id-2',
      version: '0.2.0',
      image: 'xcord/fed:0.2.0',
      releaseNotes: null,
      isMinimumVersion: false,
      minimumEnforcementDate: null,
      publishedAt: '2026-02-01T00:00:00Z',
    },
  ],
  upgradeHistory: [],
};

describe('UpdatesTab', () => {
  it('renders without crashing', async () => {
    mockFetch({
      'GET /api/v1/admin/system/version': () => ({ status: 200, body: baseResponse }),
    });
    const { getByTestId } = render(() => <UpdatesTab />);
    expect(getByTestId('updates-tab')).toBeInTheDocument();
  });

  it('loads and displays the current version on mount', async () => {
    mockFetch({
      'GET /api/v1/admin/system/version': () => ({ status: 200, body: baseResponse }),
    });
    const { findByTestId } = render(() => <UpdatesTab />);
    const cur = await findByTestId('updates-current-version');
    expect(cur).toHaveTextContent('v0.1.0');
  });

  it('shows the load error banner when the API request fails', async () => {
    mockFetch({
      'GET /api/v1/admin/system/version': () => ({
        status: 500,
        body: { error: 'Boom' },
      }),
    });
    const { findByTestId } = render(() => <UpdatesTab />);
    const err = await findByTestId('updates-load-error');
    expect(err.textContent).toBeTruthy();
  });

  it('shows the hub-disconnected banner when hubConnected is false', async () => {
    mockFetch({
      'GET /api/v1/admin/system/version': () => ({
        status: 200,
        body: { ...baseResponse, hubConnected: false },
      }),
    });
    const { findByTestId } = render(() => <UpdatesTab />);
    expect(await findByTestId('updates-hub-disconnected')).toBeInTheDocument();
  });

  it('shows the no-versions placeholder when no available versions exist', async () => {
    mockFetch({
      'GET /api/v1/admin/system/version': () => ({
        status: 200,
        body: { ...baseResponse, availableVersions: [] },
      }),
    });
    const { findByTestId } = render(() => <UpdatesTab />);
    expect(await findByTestId('updates-no-versions')).toBeInTheDocument();
  });

  it('selecting a non-current version reveals the upgrade button', async () => {
    mockFetch({
      'GET /api/v1/admin/system/version': () => ({ status: 200, body: baseResponse }),
    });
    const { findByTestId } = render(() => <UpdatesTab />);
    // Pre-selected current (0.1.0) means upgrade button hidden initially.
    const item = await findByTestId('updates-version-item-0.2.0');
    fireEvent.click(item);
    const btn = await findByTestId('updates-upgrade-button');
    expect(btn).toHaveTextContent('Update to this version');
  });

  it('toggles the upgrade history section when clicked', async () => {
    mockFetch({
      'GET /api/v1/admin/system/version': () => ({ status: 200, body: baseResponse }),
    });
    const { findByTestId, queryByTestId } = render(() => <UpdatesTab />);
    const toggle = await findByTestId('updates-history-toggle');
    expect(queryByTestId('updates-history-list')).toBeNull();
    fireEvent.click(toggle);
    await waitFor(() => expect(queryByTestId('updates-history-list')).toBeInTheDocument());
  });
});
