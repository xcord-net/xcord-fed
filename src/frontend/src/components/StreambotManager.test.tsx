import { describe, it, expect, beforeEach } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import StreambotManager from './StreambotManager';
import { useStreambot } from '../stores/streambot.store';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleBot = {
  id: 'sb-1',
  channelId: 'c-1',
  name: 'Main YouTube',
  platform: 'YouTube' as const,
  rtmpUrl: 'rtmp://a.rtmp.youtube.com/live2',
  isDefault: true,
  hasStreamKey: true,
  createdAt: '2025-01-01T00:00:00Z',
};

describe('StreambotManager', () => {
  beforeEach(() => {
    useStreambot().reset();
  });

  it('renders the Streambots heading and Add button', async () => {
    mockFetch({
      'GET /api/v1/channels/c-1/streambots': () => ({ status: 200, body: [] }),
    });
    const { findByText, findByTestId } = render(() => (
      <StreambotManager channelId="c-1" />
    ));
    expect(await findByText('Streambots')).toBeInTheDocument();
    expect(await findByTestId('streambot-add-button')).toBeInTheDocument();
  });

  it('shows the empty placeholder when no bots are configured', async () => {
    mockFetch({
      'GET /api/v1/channels/c-1/streambots': () => ({ status: 200, body: [] }),
    });
    const { findByText } = render(() => <StreambotManager channelId="c-1" />);
    expect(
      await findByText(/No streambots configured for this channel/i),
    ).toBeInTheDocument();
  });

  it('renders a streambot row with name and platform when bots are returned', async () => {
    mockFetch({
      'GET /api/v1/channels/c-1/streambots': () => ({ status: 200, body: [sampleBot] }),
    });
    const { findByTestId, findByText } = render(() => (
      <StreambotManager channelId="c-1" />
    ));
    expect(await findByTestId('streambot-row-sb-1')).toBeInTheDocument();
    expect(await findByText('Main YouTube')).toBeInTheDocument();
    expect(await findByText('YouTube')).toBeInTheDocument();
  });

  it('clicking Add Streambot reveals the create form with Name input', async () => {
    mockFetch({
      'GET /api/v1/channels/c-1/streambots': () => ({ status: 200, body: [] }),
    });
    const { findByTestId } = render(() => <StreambotManager channelId="c-1" />);
    fireEvent.click(await findByTestId('streambot-add-button'));
    expect(await findByTestId('streambot-name-input')).toBeInTheDocument();
    expect(await findByTestId('streambot-rtmp-input')).toBeInTheDocument();
    expect(await findByTestId('streambot-key-input')).toBeInTheDocument();
  });

  it('submitting the form with no name shows a validation error', async () => {
    mockFetch({
      'GET /api/v1/channels/c-1/streambots': () => ({ status: 200, body: [] }),
    });
    const { findByTestId, container } = render(() => (
      <StreambotManager channelId="c-1" />
    ));
    fireEvent.click(await findByTestId('streambot-add-button'));
    // Clear the auto-filled rtmp URL so we test the name-required path.
    fireEvent.input(await findByTestId('streambot-rtmp-input'), {
      target: { value: '' },
    });
    const form = container.querySelector('form') as HTMLFormElement;
    fireEvent.submit(form);
    await waitFor(() => expect(container.textContent).toMatch(/Name is required/i));
  });

  it('valid create posts to the streambots endpoint', async () => {
    const calls = mockFetch({
      'GET /api/v1/channels/c-1/streambots': () => ({ status: 200, body: [] }),
      'POST /api/v1/channels/c-1/streambots': () => ({ status: 200, body: sampleBot }),
    });
    const { findByTestId, container } = render(() => (
      <StreambotManager channelId="c-1" />
    ));
    fireEvent.click(await findByTestId('streambot-add-button'));
    fireEvent.input(await findByTestId('streambot-name-input'), {
      target: { value: 'Main YouTube' },
    });
    fireEvent.input(await findByTestId('streambot-key-input'), {
      target: { value: 'live_secret_key' },
    });
    const form = container.querySelector('form') as HTMLFormElement;
    fireEvent.submit(form);
    await waitFor(() =>
      expect(
        calls.calls.some(
          c => c.method === 'POST' && c.url === '/api/v1/channels/c-1/streambots',
        ),
      ).toBe(true),
    );
  });
});
