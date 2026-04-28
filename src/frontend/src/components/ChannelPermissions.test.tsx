import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ChannelPermissions, {
  cyclePermissionState,
  permissionStateColor,
  permissionStateLabel,
  createDefaultPermissions,
  ALL_PERMISSIONS,
} from './ChannelPermissions';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleData = {
  overrides: [
    {
      subjectType: 'Group' as const,
      subjectId: 'g-1',
      subjectName: 'Moderators',
      permissions: createDefaultPermissions(),
    },
    {
      subjectType: 'Member' as const,
      subjectId: 'u-1',
      subjectName: 'alice',
      permissions: createDefaultPermissions(),
    },
  ],
};

describe('ChannelPermissions pure helpers', () => {
  it('cyclePermissionState rotates Inherit -> Allow -> Deny -> Inherit', () => {
    expect(cyclePermissionState('Inherit')).toBe('Allow');
    expect(cyclePermissionState('Allow')).toBe('Deny');
    expect(cyclePermissionState('Deny')).toBe('Inherit');
  });

  it('permissionStateColor returns distinct classes for Allow/Deny/Inherit', () => {
    expect(permissionStateColor('Allow')).toContain('green');
    expect(permissionStateColor('Deny')).toContain('red');
    expect(permissionStateColor('Inherit')).not.toBe(permissionStateColor('Allow'));
  });

  it('permissionStateLabel returns the state itself', () => {
    expect(permissionStateLabel('Allow')).toBe('Allow');
  });

  it('createDefaultPermissions seeds every known permission to Inherit', () => {
    const perms = createDefaultPermissions();
    for (const key of ALL_PERMISSIONS) {
      expect(perms[key]).toBe('Inherit');
    }
  });
});

describe('ChannelPermissions', () => {
  it('renders the heading', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/channels/c-1/permissions': () => ({
        status: 200,
        body: { overrides: [] },
      }),
    });
    const { findByTestId } = render(() => (
      <ChannelPermissions serverId="s-1" channelId="c-1" />
    ));
    expect(await findByTestId('channel-permissions-heading')).toHaveTextContent(
      'Channel Permissions',
    );
  });

  it('shows placeholder when no override is selected', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/channels/c-1/permissions': () => ({
        status: 200,
        body: sampleData,
      }),
    });
    const { findByTestId } = render(() => (
      <ChannelPermissions serverId="s-1" channelId="c-1" />
    ));
    expect(await findByTestId('channel-permissions-placeholder')).toHaveTextContent(
      /Select a group or member/i,
    );
  });

  it('shows error banner when load fails', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/channels/c-1/permissions': () => ({
        status: 500,
        body: { message: 'Boom' },
      }),
    });
    const { findByText } = render(() => (
      <ChannelPermissions serverId="s-1" channelId="c-1" />
    ));
    expect(
      await findByText(/Failed to load permissions|Boom/),
    ).toBeInTheDocument();
  });

  it('selecting a group shows the permission grid', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/channels/c-1/permissions': () => ({
        status: 200,
        body: sampleData,
      }),
    });
    const { findByText, findByTestId } = render(() => (
      <ChannelPermissions serverId="s-1" channelId="c-1" />
    ));
    fireEvent.click(await findByText('Moderators'));
    expect(await findByTestId('channel-perm-row-ViewChannel')).toBeInTheDocument();
    expect(await findByTestId('channel-permissions-save-button')).toBeInTheDocument();
  });

  it('clicking Allow on a permission saves with the new state', async () => {
    const calls = mockFetch({
      'GET /api/v1/servers/s-1/channels/c-1/permissions': () => ({
        status: 200,
        body: sampleData,
      }),
      'PUT /api/v1/servers/s-1/channels/c-1/permissions': () => ({
        status: 200,
        body: sampleData,
      }),
    });
    const { findByText, findByTestId } = render(() => (
      <ChannelPermissions serverId="s-1" channelId="c-1" />
    ));
    fireEvent.click(await findByText('Moderators'));
    fireEvent.click(await findByTestId('channel-perm-ViewChannel-allow'));
    fireEvent.click(await findByTestId('channel-permissions-save-button'));
    await waitFor(() => {
      const put = calls.calls.find(c => c.method === 'PUT');
      expect(put).toBeDefined();
      const body = put!.body as { subjectId: string; permissions: Record<string, string> };
      expect(body.subjectId).toBe('g-1');
      expect(body.permissions.ViewChannel).toBe('Allow');
    });
  });
});
