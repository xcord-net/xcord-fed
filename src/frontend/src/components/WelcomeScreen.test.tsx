import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import WelcomeScreen, {
  validateWelcomeDescription,
  validateWelcomeChannel,
  welcomeChannelCount,
} from './WelcomeScreen';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleConfig = {
  isEnabled: true,
  description: 'Welcome to our friendly server!',
  channels: [
    {
      channelId: 'c-1',
      channelName: 'general',
      description: 'Main chat channel',
      emojiName: '👋',
    },
  ],
};

describe('WelcomeScreen pure helpers', () => {
  it('validateWelcomeDescription rejects empty', () => {
    expect(validateWelcomeDescription('')).toMatch(/required/i);
    expect(validateWelcomeDescription('   ')).toMatch(/required/i);
  });

  it('validateWelcomeDescription rejects > 500 chars', () => {
    expect(validateWelcomeDescription('a'.repeat(501))).toMatch(/500/);
  });

  it('validateWelcomeDescription accepts a valid description', () => {
    expect(validateWelcomeDescription('Hello!')).toBeNull();
  });

  it('validateWelcomeChannel checks channelId and description', () => {
    expect(validateWelcomeChannel({ channelId: '', description: 'desc' })).toMatch(
      /Channel ID/i,
    );
    expect(validateWelcomeChannel({ channelId: 'c-1', description: '' })).toMatch(
      /Channel description/i,
    );
    expect(
      validateWelcomeChannel({ channelId: 'c-1', description: 'desc' }),
    ).toBeNull();
  });

  it('welcomeChannelCount returns the channel list length', () => {
    expect(welcomeChannelCount(sampleConfig as never)).toBe(1);
  });
});

describe('WelcomeScreen', () => {
  it('renders the Welcome Screen header', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/welcome-screen': () => ({ status: 200, body: sampleConfig }),
    });
    const { findByText } = render(() => <WelcomeScreen serverId="s-1" />);
    expect(await findByText('Welcome Screen')).toBeInTheDocument();
  });

  it('renders the Enabled badge and description from config', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/welcome-screen': () => ({ status: 200, body: sampleConfig }),
    });
    const { findByText } = render(() => <WelcomeScreen serverId="s-1" />);
    expect(await findByText('Enabled')).toBeInTheDocument();
    expect(await findByText('Welcome to our friendly server!')).toBeInTheDocument();
  });

  it('renders a configured recommended channel', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/welcome-screen': () => ({ status: 200, body: sampleConfig }),
    });
    const { findByText } = render(() => <WelcomeScreen serverId="s-1" />);
    expect(await findByText('#general')).toBeInTheDocument();
    expect(await findByText('Main chat channel')).toBeInTheDocument();
  });

  it('shows "No welcome screen configured" when load fails', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/welcome-screen': () => ({ status: 500, body: { message: 'fail' } }),
    });
    const { findByText } = render(() => <WelcomeScreen serverId="s-1" />);
    expect(await findByText(/No welcome screen configured/i)).toBeInTheDocument();
  });

  it('reveals the edit form when an owner clicks Edit', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/welcome-screen': () => ({ status: 200, body: sampleConfig }),
    });
    const { findByText, findByLabelText } = render(() => (
      <WelcomeScreen serverId="s-1" isOwner={true} />
    ));
    fireEvent.click(await findByText('Edit'));
    expect(await findByText('Enable Welcome Screen')).toBeInTheDocument();
    expect(await findByLabelText('Welcome screen description')).toBeInTheDocument();
    await waitFor(() => expect(true).toBe(true));
  });
});
