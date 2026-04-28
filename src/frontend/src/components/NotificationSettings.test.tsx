import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import NotificationSettings from './NotificationSettings';
import { mockFetch } from '../tests/helpers/mockFetch';
import { useNotifications } from '../stores/notification.store';
import { useServers } from '../stores/server.store';

vi.mock('../services/notification.service', () => ({
  requestPermission: vi.fn(async () => 'granted' as NotificationPermission),
}));

describe('NotificationSettings', () => {
  beforeEach(() => {
    useNotifications().reset();
    useServers().reset();
  });

  it('renders the heading on mount', () => {
    mockFetch({
      'GET /api/v1/users/@me/notification-settings': () => ({ status: 200, body: { muteAll: false, settings: [] } }),
      'GET /api/v1/users/@me/servers': () => ({ status: 200, body: { servers: [] } }),
    });
    const { getByTestId } = render(() => <NotificationSettings />);
    expect(getByTestId('notification-settings-heading')).toHaveTextContent('Notification Settings');
  });

  it('renders General toggles after settings load', async () => {
    mockFetch({
      'GET /api/v1/users/@me/notification-settings': () => ({ status: 200, body: { muteAll: false, settings: [] } }),
      'GET /api/v1/users/@me/servers': () => ({ status: 200, body: { servers: [] } }),
    });
    const { findByTestId } = render(() => <NotificationSettings />);
    expect(await findByTestId('notification-mute-all-checkbox')).toBeInTheDocument();
  });

  it('reflects muteAll value from the store', async () => {
    mockFetch({
      'GET /api/v1/users/@me/notification-settings': () => ({ status: 200, body: { muteAll: true, settings: [] } }),
      'GET /api/v1/users/@me/servers': () => ({ status: 200, body: { servers: [] } }),
    });
    const { findByTestId } = render(() => <NotificationSettings />);
    const checkbox = await findByTestId('notification-mute-all-checkbox') as HTMLInputElement;
    await waitFor(() => expect(checkbox.checked).toBe(true));
  });

  it('toggling muteAll calls PUT /notification-settings', async () => {
    const calls = mockFetch({
      'GET /api/v1/users/@me/notification-settings': () => ({ status: 200, body: { muteAll: false, settings: [] } }),
      'PUT /api/v1/users/@me/notification-settings': () => ({ status: 204, body: null }),
      'GET /api/v1/users/@me/servers': () => ({ status: 200, body: { servers: [] } }),
    });
    const { findByTestId } = render(() => <NotificationSettings />);
    const checkbox = await findByTestId('notification-mute-all-checkbox') as HTMLInputElement;
    checkbox.checked = true;
    fireEvent.change(checkbox);
    await waitFor(() =>
      expect(calls.calls.some(c => c.method === 'PUT' && c.url === '/api/v1/users/@me/notification-settings')).toBe(true),
    );
  });

  it('renders the Server Overrides section when servers are loaded', async () => {
    mockFetch({
      'GET /api/v1/users/@me/notification-settings': () => ({ status: 200, body: { muteAll: false, settings: [] } }),
      'GET /api/v1/users/@me/servers': () => ({
        status: 200,
        body: { servers: [{ id: 's-1', name: 'My Server', ownerId: 'u-1' }] },
      }),
    });
    const { findByLabelText } = render(() => <NotificationSettings />);
    expect(await findByLabelText('Notification settings for My Server')).toBeInTheDocument();
  });
});
