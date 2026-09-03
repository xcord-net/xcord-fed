import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import WebhookManager, {
  validateWebhookUrl,
  validateWebhookName,
  toggleEvent,
} from './WebhookManager';
import { mockFetch } from '../tests/helpers/mockFetch';

// The shape the server actually sends: no name, `eventTypes`/`isActive`, and no
// secret in a listing - it exists only on the webhook you just created.
const sampleWebhook = {
  id: 'wh-1',
  serverId: 's-1',
  targetUrl: 'https://example.com/hook',
  eventTypes: ['MessageCreated', 'MemberJoined'] as const,
  isActive: true,
  createdByUserId: 'u-1',
  createdAt: '2025-01-01T00:00:00Z',
};

describe('WebhookManager pure helpers', () => {
  it('validateWebhookUrl rejects empty / non-http(s) urls', () => {
    expect(validateWebhookUrl('')).toMatch(/required/);
    expect(validateWebhookUrl('ftp://x.com')).toMatch(/http or https/);
    expect(validateWebhookUrl('not-a-url')).toMatch(/Invalid/);
  });

  it('validateWebhookUrl accepts a valid https URL', () => {
    expect(validateWebhookUrl('https://example.com/path')).toBeNull();
  });

  it('validateWebhookName rejects empty and over-long names', () => {
    expect(validateWebhookName('')).toMatch(/required/);
    expect(validateWebhookName('x'.repeat(65))).toMatch(/64 characters/);
    expect(validateWebhookName('Audit Logger')).toBeNull();
  });

  it('toggleEvent adds the event when missing and removes it when present', () => {
    expect(toggleEvent([], 'MessageCreated')).toEqual(['MessageCreated']);
    expect(toggleEvent(['MessageCreated'], 'MessageCreated')).toEqual([]);
  });
});

describe('WebhookManager', () => {
  it('renders the Outgoing Webhooks heading', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/outgoing-webhooks': () => ({ status: 200, body: [] }),
    });
    const { findByText } = render(() => <WebhookManager serverId="s-1" />);
    expect(await findByText('Outgoing Webhooks')).toBeInTheDocument();
  });

  it('shows the empty state when no webhooks are returned', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/outgoing-webhooks': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => <WebhookManager serverId="s-1" />);
    expect(await findByTestId('webhook-list-empty-state')).toBeInTheDocument();
  });

  it('renders a webhook item by its destination', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/outgoing-webhooks': () => ({
        status: 200,
        body: [sampleWebhook],
      }),
    });
    const { findByTestId, queryByTestId } = render(() => <WebhookManager serverId="s-1" />);
    // One line, not two: a webhook has no name, so its destination is shown once.
    expect(await findByTestId('webhook-url-wh-1')).toHaveTextContent(
      'https://example.com/hook',
    );
    expect(queryByTestId('webhook-name-wh-1')).toBeNull();
  });

  it('shows error banner when load fails', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/outgoing-webhooks': () => ({
        status: 500,
        body: { message: 'Boom' },
      }),
    });
    const { findByText } = render(() => <WebhookManager serverId="s-1" />);
    expect(await findByText(/Failed to load webhooks|Boom/)).toBeInTheDocument();
  });

  it('toggling Add Webhook reveals the create form', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/outgoing-webhooks': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => <WebhookManager serverId="s-1" />);
    fireEvent.click(await findByTestId('create-webhook-button'));
    expect(await findByTestId('webhook-create-form')).toBeInTheDocument();
  });

  it('offers no secret to reveal for a webhook loaded from the server', async () => {
    // The listing does not carry a secret - it is returned once, when the
    // webhook is created - so there is nothing here to reveal.
    mockFetch({
      'GET /api/v1/servers/s-1/outgoing-webhooks': () => ({
        status: 200,
        body: [sampleWebhook],
      }),
    });
    const { findByTestId, queryByTestId } = render(() => <WebhookManager serverId="s-1" />);
    await findByTestId('webhook-item-wh-1');
    expect(queryByTestId('webhook-secret-row-wh-1')).toBeNull();
  });

  it('submitting the create form with no events shows a validation error', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/outgoing-webhooks': () => ({ status: 200, body: [] }),
    });
    const { findByTestId, container } = render(() => <WebhookManager serverId="s-1" />);
    fireEvent.click(await findByTestId('create-webhook-button'));
    fireEvent.input(await findByTestId('webhook-url-input'), {
      target: { value: 'https://example.com/hook' },
    });
    const form = container.querySelector(
      '[data-testid="webhook-create-form"] form',
    ) as HTMLFormElement;
    fireEvent.submit(form);
    await waitFor(() =>
      expect(container.textContent).toMatch(/Select at least one event/),
    );
  });
});
