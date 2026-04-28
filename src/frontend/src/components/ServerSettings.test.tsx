import { describe, it, expect, beforeEach, vi } from 'vitest';
import { fireEvent, waitFor } from '@solidjs/testing-library';
import ServerSettings from './ServerSettings';
import { useServers } from '../stores/server.store';
import { useChannels } from '../stores/channel.store';
import { useAuth } from '../stores/auth.store';
import { renderWithRouter } from '../tests/helpers/renderWithRouter';
import { mockFetch } from '../tests/helpers/mockFetch';

// Stub heavy subcomponents so this test focuses purely on the ServerSettings shell
// (tab navigation + Overview form save).
vi.mock('./AutomodManager', () => ({ default: () => <div data-testid="stub-automod" /> }));
vi.mock('./BanManager', () => ({ default: () => <div data-testid="stub-bans" /> }));
vi.mock('./AuditLogViewer', () => ({ default: () => <div data-testid="stub-audit" /> }));
vi.mock('./EmojiManager', () => ({ default: () => <div data-testid="stub-emoji" /> }));
vi.mock('./StickerPicker', () => ({ default: () => <div data-testid="stub-stickers" /> }));
vi.mock('./VanityInvite', () => ({ default: () => <div data-testid="stub-vanity" /> }));
vi.mock('./ServerTemplates', () => ({ default: () => <div data-testid="stub-templates" /> }));
vi.mock('./ServerBoost', () => ({ default: () => <div data-testid="stub-boost" /> }));
vi.mock('./ServerInsights', () => ({ default: () => <div data-testid="stub-insights" /> }));
vi.mock('./InviteManager', () => ({ default: () => <div data-testid="stub-invites" /> }));
vi.mock('./AppDirectory', () => ({ default: () => <div data-testid="stub-app-directory" /> }));
vi.mock('./BotsTab', () => ({ default: () => <div data-testid="stub-bots" /> }));
vi.mock('./OwnershipTransfer', () => ({ default: () => <div data-testid="stub-ownership" /> }));
vi.mock('./WelcomeScreen', () => ({ default: () => <div data-testid="stub-welcome" /> }));
vi.mock('./UpdatesTab', () => ({ default: () => <div data-testid="stub-updates" /> }));

describe('ServerSettings', () => {
  beforeEach(() => {
    useServers().reset();
    useChannels().reset();
    useAuth().reset();
  });

  function emptyServersFetch() {
    return mockFetch({
      'GET /api/v1/users/@me/servers': () => ({
        status: 200,
        body: { servers: [] },
      }),
    });
  }

  it('renders the dialog with the Server Settings header', () => {
    emptyServersFetch();
    const { getByText, getByTestId } = renderWithRouter(() => (
      <ServerSettings serverId="s-1" onClose={() => {}} />
    ));
    expect(getByText('Server Settings')).toBeInTheDocument();
    expect(getByTestId('server-settings-close-button')).toBeInTheDocument();
  });

  it('renders the Overview tab and the server name input by default', () => {
    emptyServersFetch();
    const { getByTestId } = renderWithRouter(() => (
      <ServerSettings serverId="s-1" onClose={() => {}} />
    ));
    expect(getByTestId('server-settings-tab-overview')).toBeInTheDocument();
    expect(getByTestId('server-name-input')).toBeInTheDocument();
    expect(getByTestId('server-settings-save-button')).toBeInTheDocument();
  });

  it('invokes the close handler when the close button is clicked', () => {
    emptyServersFetch();
    let closed = false;
    const { getByTestId } = renderWithRouter(() => (
      <ServerSettings serverId="s-1" onClose={() => { closed = true; }} />
    ));
    fireEvent.click(getByTestId('server-settings-close-button'));
    expect(closed).toBe(true);
  });

  it('submits PATCH /api/v1/servers/:id when the form is submitted', async () => {
    const calls = mockFetch({
      'GET /api/v1/users/@me/servers': () => ({ status: 200, body: { servers: [] } }),
      'PATCH /api/v1/servers/s-1': () => ({ status: 204, body: null }),
    });
    const { getByTestId, container } = renderWithRouter(() => (
      <ServerSettings serverId="s-1" onClose={() => {}} />
    ));
    const input = getByTestId('server-name-input') as HTMLInputElement;
    fireEvent.input(input, { target: { value: 'My Cool Server' } });
    const form = container.querySelector('form') as HTMLFormElement;
    fireEvent.submit(form);
    await waitFor(() =>
      expect(
        calls.calls.some((c) => c.method === 'PATCH' && c.url === '/api/v1/servers/s-1'),
      ).toBe(true),
    );
  });

  it('renders an error banner when save fails', async () => {
    mockFetch({
      'GET /api/v1/users/@me/servers': () => ({ status: 200, body: { servers: [] } }),
      'PATCH /api/v1/servers/s-1': () => ({ status: 500, body: { message: 'Server save failed' } }),
    });
    const { getByTestId, container, findByRole } = renderWithRouter(() => (
      <ServerSettings serverId="s-1" onClose={() => {}} />
    ));
    fireEvent.input(getByTestId('server-name-input'), { target: { value: 'Boom' } });
    const form = container.querySelector('form') as HTMLFormElement;
    fireEvent.submit(form);
    const alert = await findByRole('alert');
    expect(alert.textContent).toMatch(/Server save failed|Failed to save settings/);
  });

  it('does not show owner-only tabs (e.g. Audit Log) when user is not the owner', () => {
    emptyServersFetch();
    const { queryByTestId } = renderWithRouter(() => (
      <ServerSettings serverId="s-1" onClose={() => {}} />
    ));
    expect(queryByTestId('server-settings-tab-audit-log')).toBeNull();
    expect(queryByTestId('server-settings-tab-bans')).toBeNull();
  });
});
