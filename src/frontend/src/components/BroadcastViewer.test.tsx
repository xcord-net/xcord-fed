import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@solidjs/testing-library';

// hls.js needs MediaSource which is not available in jsdom; stub it.
vi.mock('hls.js', () => {
  // vitest 4 requires `function` (not arrow) for mocks used as constructors.
  const Hls = vi.fn(function () {
    return {
      loadSource: vi.fn(),
      attachMedia: vi.fn(),
      on: vi.fn(),
      destroy: vi.fn(),
      startLoad: vi.fn(),
    };
  }) as unknown as { isSupported: () => boolean; Events: Record<string, string> };
  Hls.isSupported = () => true;
  Hls.Events = { MANIFEST_PARSED: 'manifestParsed', ERROR: 'hlsError' };
  return { default: Hls };
});

import BroadcastViewer from './BroadcastViewer';
import type { Broadcast } from '../stores/broadcast.store';

function makeBroadcast(over: Partial<Broadcast> = {}): Broadcast {
  return {
    id: 'b-1',
    channelId: 'ch-1',
    hostUserId: 'host-1',
    layoutPreset: 'Grid',
    status: 'Live',
    hlsUrl: 'https://example.com/stream.m3u8',
    roomName: 'room-1',
    startedAt: '2025-01-01T00:00:00Z',
    stageSlots: [],
    streambots: [],
    ...over,
  };
}

describe('BroadcastViewer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders viewer container with LIVE badge', () => {
    const { getByTestId, getByText } = render(() => (
      <BroadcastViewer broadcast={makeBroadcast()} />
    ));
    expect(getByTestId('broadcast-viewer')).toBeInTheDocument();
    expect(getByText('LIVE')).toBeInTheDocument();
  });

  it('renders a <video> element with autoplay/muted/playsinline/controls', () => {
    const { container } = render(() => (
      <BroadcastViewer broadcast={makeBroadcast()} />
    ));
    const video = container.querySelector('video') as HTMLVideoElement;
    expect(video).not.toBeNull();
    // jsdom doesn't always reflect boolean attributes on the property; check attributes.
    expect(video.hasAttribute('autoplay')).toBe(true);
    expect(video.hasAttribute('muted')).toBe(true);
    expect(video.hasAttribute('controls')).toBe(true);
    expect(video.hasAttribute('playsinline')).toBe(true);
  });

  it('initializes Hls when supported and loads the broadcast hlsUrl', async () => {
    const Hls = (await import('hls.js')).default as unknown as ReturnType<typeof vi.fn>;
    render(() => <BroadcastViewer broadcast={makeBroadcast({ hlsUrl: 'https://x.test/s.m3u8' })} />);
    expect(Hls).toHaveBeenCalled();
    const instance = Hls.mock.results[0].value;
    expect(instance.loadSource).toHaveBeenCalledWith('https://x.test/s.m3u8');
    expect(instance.attachMedia).toHaveBeenCalled();
  });

  it('does not render error UI in the happy path', () => {
    const { container } = render(() => (
      <BroadcastViewer broadcast={makeBroadcast()} />
    ));
    expect(container.textContent).not.toContain('not supported');
  });
});
