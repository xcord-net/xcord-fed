import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import IdlePanel from './IdlePanel';

function baseProps(over: Partial<Parameters<typeof IdlePanel>[0]> = {}) {
  return {
    selectedLayout: 'Grid' as const,
    onSelectLayout: vi.fn(),
    streambots: [],
    selectedStreambotIds: new Set<string>(),
    onToggleStreambot: vi.fn(),
    starting: false,
    error: null,
    onStart: vi.fn(),
    ...over,
  };
}

describe('IdlePanel', () => {
  it('renders without crashing with minimal props', () => {
    const { getByTestId } = render(() => <IdlePanel {...baseProps()} />);
    expect(getByTestId('broadcast-start-button')).toBeInTheDocument();
  });

  it('renders the heading and subheading', () => {
    const { container } = render(() => <IdlePanel {...baseProps()} />);
    expect(container.textContent).toContain('Start a Broadcast');
    expect(container.textContent).toContain('Choose a layout and restream destinations');
  });

  it('renders an empty-state when no streambots are configured', () => {
    const { container } = render(() => <IdlePanel {...baseProps()} />);
    expect(container.querySelector('[data-testid="idle-panel-streambots-empty"]')).not.toBeNull();
  });

  it('renders streambot rows with checkboxes when streambots are provided', () => {
    const streambots = [
      { id: 'sb-1', name: 'Twitch Relay', platform: 'twitch' },
      { id: 'sb-2', name: 'YouTube Relay', platform: 'youtube' },
    ];
    const { getByTestId } = render(() => (
      <IdlePanel {...baseProps({ streambots, selectedStreambotIds: new Set(['sb-1']) })} />
    ));
    const cb1 = getByTestId('streambot-checkbox-sb-1') as HTMLInputElement;
    const cb2 = getByTestId('streambot-checkbox-sb-2') as HTMLInputElement;
    expect(cb1.checked).toBe(true);
    expect(cb2.checked).toBe(false);
  });

  it('invokes onSelectLayout when a layout card is clicked', () => {
    const onSelectLayout = vi.fn();
    const { getByTestId } = render(() => (
      <IdlePanel {...baseProps({ onSelectLayout })} />
    ));
    fireEvent.click(getByTestId('broadcast-layout-spotlight'));
    expect(onSelectLayout).toHaveBeenCalledWith('Spotlight');
  });

  it('invokes onToggleStreambot when a checkbox is toggled', () => {
    const onToggleStreambot = vi.fn();
    const streambots = [{ id: 'sb-1', name: 'Twitch Relay', platform: 'twitch' }];
    const { getByTestId } = render(() => (
      <IdlePanel {...baseProps({ streambots, onToggleStreambot })} />
    ));
    fireEvent.click(getByTestId('streambot-checkbox-sb-1'));
    expect(onToggleStreambot).toHaveBeenCalledWith('sb-1');
  });

  it('invokes onStart when the start button is clicked', () => {
    const onStart = vi.fn();
    const { getByTestId } = render(() => (
      <IdlePanel {...baseProps({ onStart })} />
    ));
    fireEvent.click(getByTestId('broadcast-start-button'));
    expect(onStart).toHaveBeenCalledTimes(1);
  });

  it('disables the start button and shows Starting... while starting is true', () => {
    const { getByTestId } = render(() => (
      <IdlePanel {...baseProps({ starting: true })} />
    ));
    const btn = getByTestId('broadcast-start-button') as HTMLButtonElement;
    expect(btn).toBeDisabled();
    expect(btn).toHaveTextContent('Starting...');
  });

  it('renders an error alert when error prop is set', () => {
    const { getByRole } = render(() => (
      <IdlePanel {...baseProps({ error: 'LiveKit handshake failed' })} />
    ));
    expect(getByRole('alert')).toHaveTextContent('LiveKit handshake failed');
  });
});
