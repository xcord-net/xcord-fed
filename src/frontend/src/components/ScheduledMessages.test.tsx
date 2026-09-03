import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ScheduledMessages from './ScheduledMessages';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleMessage = {
  id: 'm-1',
  conversationId: 'c-1',
  authorId: 'u-1',
  content: 'Reminder!',
  scheduledAt: new Date(Date.now() + 3_600_000).toISOString(),
  sentAt: null,
};

describe('ScheduledMessages', () => {
  it('renders the panel header', async () => {
    mockFetch({
      'GET /api/v1/channels/ch-1/scheduled-messages': () => ({ status: 200, body: [] }),
    });
    const { findByText } = render(() => <ScheduledMessages channelId="ch-1" />);
    expect(await findByText('Scheduled Messages')).toBeInTheDocument();
  });

  it('shows the empty state when there are no scheduled messages', async () => {
    mockFetch({
      'GET /api/v1/channels/ch-1/scheduled-messages': () => ({ status: 200, body: [] }),
    });
    const { findByText, findByTestId } = render(() => <ScheduledMessages channelId="ch-1" />);
    expect(await findByTestId('scheduled-messages-empty')).toBeInTheDocument();
  });

  it('renders a scheduled message with its content', async () => {
    mockFetch({
      'GET /api/v1/channels/ch-1/scheduled-messages': () => ({ status: 200, body: [sampleMessage] }),
    });
    const { findByText } = render(() => <ScheduledMessages channelId="ch-1" />);
    expect(await findByText('Reminder!')).toBeInTheDocument();
  });

  it('shows an error banner when load fails', async () => {
    mockFetch({
      'GET /api/v1/channels/ch-1/scheduled-messages': () => ({ status: 500, body: { message: 'Boom' } }),
    });
    const { findByRole } = render(() => <ScheduledMessages channelId="ch-1" />);
    const alert = await findByRole('alert');
    expect(alert.textContent).toMatch(/Boom|Failed to load scheduled messages/);
  });

  it('opens a confirmation dialog when Cancel is clicked', async () => {
    mockFetch({
      'GET /api/v1/channels/ch-1/scheduled-messages': () => ({ status: 200, body: [sampleMessage] }),
    });
    const { findByText, findAllByText } = render(() => (
      <ScheduledMessages channelId="ch-1" />
    ));
    const cancelButtons = await findAllByText('Cancel');
    fireEvent.click(cancelButtons[0]);
    expect(await findByText('Cancel Scheduled Message')).toBeInTheDocument();
    expect(await findByText('Keep')).toBeInTheDocument();
  });

  it('issues DELETE when the user confirms cancellation', async () => {
    const calls = mockFetch({
      'GET /api/v1/channels/ch-1/scheduled-messages': () => ({ status: 200, body: [sampleMessage] }),
      'DELETE /api/v1/channels/ch-1/scheduled-messages/m-1': () => ({ status: 204, body: null }),
    });
    const { findAllByText, findByText } = render(() => (
      <ScheduledMessages channelId="ch-1" />
    ));
    const cancelButtons = await findAllByText('Cancel');
    fireEvent.click(cancelButtons[0]);
    fireEvent.click(await findByText('Cancel Message'));
    await waitFor(() =>
      expect(calls.calls.some(c => c.method === 'DELETE' && c.url.endsWith('/scheduled-messages/m-1'))).toBe(true),
    );
  });
});
