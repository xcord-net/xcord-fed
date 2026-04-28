import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import StreambotStatusBadge from './StreambotStatusBadge';

describe('StreambotStatusBadge', () => {
  it('renders the streambot name', () => {
    const { getByTestId } = render(() => <StreambotStatusBadge name="bot-1" status="Active" />);
    expect(getByTestId('streambot-status-badge').textContent).toContain('bot-1');
  });

  it('renders without an error icon for non-Failed status', () => {
    const { getByTestId } = render(() => <StreambotStatusBadge name="bot-1" status="Active" />);
    expect(getByTestId('streambot-status-badge').textContent).not.toContain('!');
  });

  it('renders the error icon when status is Failed and error is provided', () => {
    const { getByTestId } = render(() => (
      <StreambotStatusBadge name="bot-1" status="Failed" error="boom" />
    ));
    expect(getByTestId('streambot-status-badge').textContent).toContain('!');
  });

  it('does not render error icon for Failed status without error message', () => {
    const { getByTestId } = render(() => <StreambotStatusBadge name="bot-1" status="Failed" />);
    expect(getByTestId('streambot-status-badge').textContent).not.toContain('!');
  });

  it('applies different classes per status', () => {
    const { getByTestId, unmount } = render(() => <StreambotStatusBadge name="b" status="Connecting" />);
    const connectingClass = getByTestId('streambot-status-badge').className;
    unmount();
    const second = render(() => <StreambotStatusBadge name="b" status="Ended" />);
    expect(second.getByTestId('streambot-status-badge').className).not.toBe(connectingClass);
  });
});
