import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import AutomodManager from './AutomodManager';
import { mockFetch } from '../../tests/helpers/mockFetch';
import type { AutomodRule } from './helpers';

const sampleRule: AutomodRule = {
  id: 'r-1',
  serverId: 's-1',
  name: 'Block Profanity',
  enabled: true,
  triggerType: 'Keyword',
  triggerConfig: JSON.stringify({ keywords: ['badword'], matchWholeWord: false }),
  actionType: 'BlockMessage',
  actionConfig: null,
  exemptRoleIds: null,
  exemptChannelIds: null,
  exemptBots: false,
  createdAt: '2026-01-01T00:00:00Z',
};

describe('AutomodManager', () => {
  it('renders the heading and add rule button', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/automod-rules': () => ({ status: 200, body: { rules: [] } }),
    });
    const { findByText, getByLabelText } = render(() => <AutomodManager serverId="s-1" />);
    expect(await findByText('Automod Rules')).toBeInTheDocument();
    expect(getByLabelText('Create automod rule')).toBeInTheDocument();
  });

  it('shows the empty state when there are no rules', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/automod-rules': () => ({ status: 200, body: { rules: [] } }),
    });
    const { findByText, findByTestId } = render(() => <AutomodManager serverId="s-1" />);
    expect(await findByTestId('automod-empty')).toBeInTheDocument();
  });

  it('loads and displays existing rules from the API', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/automod-rules': () => ({
        status: 200,
        body: { rules: [sampleRule] },
      }),
    });
    const { findByLabelText } = render(() => <AutomodManager serverId="s-1" />);
    expect(await findByLabelText('Automod rule: Block Profanity')).toBeInTheDocument();
  });

  it('shows an error banner when loading fails', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/automod-rules': () => ({
        status: 500,
        body: { message: 'Server error' },
      }),
    });
    const { findByRole } = render(() => <AutomodManager serverId="s-1" />);
    const alert = await findByRole('alert');
    expect(alert.textContent).toMatch(/Failed to load automod rules|Server error/);
  });

  it('opens the create form when the add button is clicked', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/automod-rules': () => ({ status: 200, body: { rules: [] } }),
    });
    const { findByLabelText, getByText } = render(() => <AutomodManager serverId="s-1" />);
    fireEvent.click(await findByLabelText('Create automod rule'));
    expect(getByText('New Automod Rule')).toBeInTheDocument();
  });

  it('creates a new rule and shows a success message', async () => {
    const created: AutomodRule = { ...sampleRule, id: 'r-2', name: 'Block Spam' };
    mockFetch({
      'GET /api/v1/servers/s-1/automod-rules': () => ({ status: 200, body: { rules: [] } }),
      'POST /api/v1/servers/s-1/automod-rules': () => ({ status: 200, body: created }),
    });
    const { findByLabelText, container, getByText } = render(() => (
      <AutomodManager serverId="s-1" />
    ));
    fireEvent.click(await findByLabelText('Create automod rule'));

    const nameInput = container.querySelector('#automod-rule-name') as HTMLInputElement;
    fireEvent.input(nameInput, { target: { value: 'Block Spam' } });

    const form = container.querySelector('form[aria-label="Create automod rule"]')!;
    fireEvent.submit(form);

    await waitFor(() => expect(getByText('Rule created successfully.')).toBeInTheDocument());
  });
});
