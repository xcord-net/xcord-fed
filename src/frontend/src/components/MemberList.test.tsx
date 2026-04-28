import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@solidjs/testing-library';
import MemberList from './MemberList';
import { useMembers } from '../stores/member.store';
import { useServers } from '../stores/server.store';
import { usePresence } from '../stores/presence.store';
import { mockFetch } from '../tests/helpers/mockFetch';
import type { Member } from '../types/member';

function makeMember(over: Partial<Member> = {}): Member {
  return {
    userId: 'u-1',
    serverId: 's-1',
    username: 'alice',
    displayName: 'Alice',
    groups: [],
    joinedAt: '2025-01-01T00:00:00Z',
    ...over,
  };
}

describe('MemberList', () => {
  beforeEach(() => {
    useMembers().reset();
    useServers().reset();
    usePresence().reset();
  });

  it('renders nothing when open is false', () => {
    const { queryByTestId } = render(() => (
      <MemberList open={false} onClose={() => undefined} />
    ));
    expect(queryByTestId('member-list-heading')).toBeNull();
  });

  it('renders the panel when open is true', () => {
    const { getByTestId } = render(() => (
      <MemberList open={true} onClose={() => undefined} />
    ));
    expect(getByTestId('member-list-heading')).toBeInTheDocument();
  });

  it('groups members under "Members" when they have no custom group', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/members': () => ({
        status: 200,
        body: [makeMember()],
      }),
    });
    await useMembers().fetchMembers('s-1');

    const { getByTestId, getByText } = render(() => (
      <MemberList open={true} onClose={() => undefined} />
    ));
    expect(getByTestId('member-group-heading')).toHaveTextContent(/Members - 1/);
    expect(getByText('Alice')).toBeInTheDocument();
  });

  it('places members in their highest-position custom group', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/members': () => ({
        status: 200,
        body: [
          makeMember({
            userId: 'u-2',
            username: 'bob',
            displayName: 'Bob',
            groups: [
              { id: 'g-1', name: 'Mods', position: 5 },
              { id: 'g-2', name: 'VIP', position: 10 },
            ],
          }),
        ],
      }),
    });
    await useMembers().fetchMembers('s-1');

    const { getByText } = render(() => (
      <MemberList open={true} onClose={() => undefined} />
    ));
    expect(getByText(/VIP - 1/)).toBeInTheDocument();
  });

  it('does not render Manage Groups / Ban actions when current user is not the server owner', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/members': () => ({
        status: 200,
        body: [makeMember()],
      }),
    });
    await useMembers().fetchMembers('s-1');

    const { queryByTestId } = render(() => (
      <MemberList open={true} onClose={() => undefined} />
    ));
    // Without seeded auth/owner, ban/manage-groups menu items must not be rendered.
    expect(queryByTestId('member-context-ban')).toBeNull();
    expect(queryByTestId('member-context-manage-groups')).toBeNull();
  });
});
