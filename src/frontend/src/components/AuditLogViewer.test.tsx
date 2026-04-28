import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import AuditLogViewer, { formatTimestamp, getActionIcon } from './AuditLogViewer';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleEntry = {
  id: 'e-1',
  actionType: 'MemberBan',
  actorId: 'u-1',
  actorUsername: 'modAlice',
  targetId: 'u-2',
  targetName: 'spamBob',
  reason: 'spam',
  createdAt: '2025-01-01T12:00:00Z',
};

describe('AuditLogViewer pure helpers', () => {
  it('getActionIcon returns the mapped emoji for known action types', () => {
    expect(getActionIcon('MemberBan')).toBe('🔨');
    expect(getActionIcon('ChannelCreate')).toBe('📢');
  });

  it('getActionIcon falls back to the clipboard emoji for unknown action types', () => {
    expect(getActionIcon('NotARealAction')).toBe('📋');
  });

  it('formatTimestamp returns a localized string containing the year', () => {
    const out = formatTimestamp('2025-01-01T12:00:00Z');
    expect(out).toMatch(/2025/);
  });
});

describe('AuditLogViewer', () => {
  it('renders the Audit Log header', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/audit-log': () => ({ status: 200, body: [] }) });
    const { findByText } = render(() => <AuditLogViewer serverId="s-1" />);
    expect(await findByText('Audit Log')).toBeInTheDocument();
  });

  it('shows the empty state when there are no entries', async () => {
    mockFetch({ 'GET /api/v1/servers/s-1/audit-log': () => ({ status: 200, body: [] }) });
    const { findByText } = render(() => <AuditLogViewer serverId="s-1" />);
    expect(await findByText('No audit log entries')).toBeInTheDocument();
  });

  it('renders an entry with actor, action, and reason', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/audit-log': () => ({ status: 200, body: [sampleEntry] }),
    });
    const { findByText, findAllByText, container } = render(() => <AuditLogViewer serverId="s-1" />);
    expect(await findByText('modAlice')).toBeInTheDocument();
    // 'MemberBan' appears in both the filter <option> and the entry's action span
    const matches = await findAllByText('MemberBan');
    expect(matches.length).toBeGreaterThan(1);
    await waitFor(() => expect(container.textContent).toContain('Reason: spam'));
  });

  it('shows an error banner when load fails', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/audit-log': () => ({ status: 500, body: { message: 'Boom' } }),
    });
    const { findByText } = render(() => <AuditLogViewer serverId="s-1" />);
    expect(await findByText(/Failed to load audit log|Boom/)).toBeInTheDocument();
  });

  it('reloads with the actionType filter applied when changing the dropdown', async () => {
    const calls = mockFetch({
      'GET /api/v1/servers/s-1/audit-log': () => ({ status: 200, body: [] }),
    });
    const { container, findByText } = render(() => <AuditLogViewer serverId="s-1" />);
    // Wait for the initial load to settle (isLoading=false), otherwise applyFilters
    // is short-circuited by the early-return inside loadEntries.
    await findByText('No audit log entries');
    const select = container.querySelector('select') as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'MemberBan' } });
    await waitFor(() => {
      expect(
        calls.calls.some(c => c.method === 'GET' && c.url.includes('actionType=MemberBan')),
      ).toBe(true);
    });
  });
});
