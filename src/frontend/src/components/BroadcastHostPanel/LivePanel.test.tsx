import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import LivePanel from './LivePanel';
import type { Broadcast } from '../../stores/broadcast.store';

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

function baseProps(over: Partial<Parameters<typeof LivePanel>[0]> = {}) {
  return {
    broadcast: makeBroadcast(),
    selectedLayout: 'Grid' as const,
    slots: [
      { slotIndex: 0, userId: null },
      { slotIndex: 1, userId: null },
    ],
    memberLookup: new Map(),
    streambots: [],
    selectedStreambotIds: new Set<string>(),
    isMuted: false,
    isCameraOff: false,
    ending: false,
    error: null,
    setPreviewRef: vi.fn(),
    onLayoutChange: vi.fn(),
    onStreambotToggle: vi.fn(),
    onStageAssign: vi.fn(),
    onStageRemove: vi.fn(),
    onToggleMute: vi.fn(),
    onToggleCamera: vi.fn(),
    onEnd: vi.fn(),
    ...over,
  };
}

describe('LivePanel', () => {
  it('renders without crashing with minimal props', () => {
    const { getByTestId } = render(() => <LivePanel {...baseProps()} />);
    expect(getByTestId('broadcast-end-button')).toBeInTheDocument();
  });

  it('renders the Broadcasting header', () => {
    const { container } = render(() => <LivePanel {...baseProps()} />);
    expect(container.textContent).toContain('Broadcasting');
  });

  it('shows Mute / Camera Off labels when both are active feeds', () => {
    const { getByTestId } = render(() => <LivePanel {...baseProps()} />);
    expect(getByTestId('broadcast-mute-button')).toHaveTextContent('Mute');
    expect(getByTestId('broadcast-camera-button')).toHaveTextContent('Camera Off');
  });

  it('shows Unmute / Camera On labels when muted and camera is off', () => {
    const { getByTestId } = render(() => (
      <LivePanel {...baseProps({ isMuted: true, isCameraOff: true })} />
    ));
    expect(getByTestId('broadcast-mute-button')).toHaveTextContent('Unmute');
    expect(getByTestId('broadcast-camera-button')).toHaveTextContent('Camera On');
  });

  it('invokes onToggleMute when the mute button is clicked', () => {
    const onToggleMute = vi.fn();
    const { getByTestId } = render(() => (
      <LivePanel {...baseProps({ onToggleMute })} />
    ));
    fireEvent.click(getByTestId('broadcast-mute-button'));
    expect(onToggleMute).toHaveBeenCalledTimes(1);
  });

  it('invokes onLayoutChange when a layout pill is clicked', () => {
    const onLayoutChange = vi.fn();
    const { getByTestId } = render(() => (
      <LivePanel {...baseProps({ onLayoutChange })} />
    ));
    fireEvent.click(getByTestId('broadcast-layout-pill-spotlight'));
    expect(onLayoutChange).toHaveBeenCalledWith('Spotlight');
  });

  it('invokes onEnd when the end-broadcast button is clicked', () => {
    const onEnd = vi.fn();
    const { getByTestId } = render(() => (
      <LivePanel {...baseProps({ onEnd })} />
    ));
    fireEvent.click(getByTestId('broadcast-end-button'));
    expect(onEnd).toHaveBeenCalledTimes(1);
  });

  it('disables the end button and shows Ending... while ending is true', () => {
    const { getByTestId } = render(() => (
      <LivePanel {...baseProps({ ending: true })} />
    ));
    const btn = getByTestId('broadcast-end-button') as HTMLButtonElement;
    expect(btn).toBeDisabled();
    expect(btn).toHaveTextContent('Ending...');
  });

  it('renders an error alert when error prop is set', () => {
    const { getByRole } = render(() => (
      <LivePanel {...baseProps({ error: 'Stage update failed' })} />
    ));
    expect(getByRole('alert')).toHaveTextContent('Stage update failed');
  });

  it('renders the empty-state when no streambots are configured', () => {
    const { container } = render(() => <LivePanel {...baseProps()} />);
    expect(container.textContent).toContain('No streambots configured');
  });
});
