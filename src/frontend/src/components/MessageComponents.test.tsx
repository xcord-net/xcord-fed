import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import MessageComponents, {
  buttonStyleClass,
  isValidButtonStyle,
  type ActionRow,
} from './MessageComponents';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('isValidButtonStyle', () => {
  it('accepts known styles', () => {
    expect(isValidButtonStyle('Primary')).toBe(true);
    expect(isValidButtonStyle('Danger')).toBe(true);
  });

  it('rejects unknown styles', () => {
    expect(isValidButtonStyle('Mystery')).toBe(false);
  });
});

describe('buttonStyleClass', () => {
  it('returns a CSS module class for each style', () => {
    expect(typeof buttonStyleClass('Primary')).toBe('string');
    expect(typeof buttonStyleClass('Link')).toBe('string');
  });
});

describe('MessageComponents', () => {
  const buttonRow: ActionRow = {
    type: 'action_row',
    components: [
      { type: 'button', button: { customId: 'b-1', label: 'Click me', style: 'Primary' } },
    ],
  };

  it('renders with the message components aria label', () => {
    const { getByLabelText } = render(() => (
      <MessageComponents messageId="m-1" interactionId="i-1" actionRows={[]} />
    ));
    expect(getByLabelText('Message components')).toBeInTheDocument();
  });

  it('renders a button with its label', () => {
    const { getByText } = render(() => (
      <MessageComponents messageId="m-1" interactionId="i-1" actionRows={[buttonRow]} />
    ));
    expect(getByText('Click me')).toBeInTheDocument();
  });

  it('posts an interaction and calls onInteract when a non-link button is clicked', async () => {
    const calls = mockFetch({
      'POST /api/v1/interactions/i-1': () => ({ status: 200, body: {} }),
    });
    const onInteract = vi.fn();
    const { getByText } = render(() => (
      <MessageComponents messageId="m-1" interactionId="i-1" actionRows={[buttonRow]} onInteract={onInteract} />
    ));
    fireEvent.click(getByText('Click me'));
    await waitFor(() => expect(onInteract).toHaveBeenCalled());
    expect(calls.calls.some(c => c.method === 'POST' && c.url.endsWith('/i-1'))).toBe(true);
  });

  it('opens link buttons in a new tab without posting interaction', () => {
    const linkRow: ActionRow = {
      type: 'action_row',
      components: [
        { type: 'button', button: { customId: 'b-2', label: 'Visit', style: 'Link', url: 'https://example.com' } },
      ],
    };
    const openSpy = vi.spyOn(window, 'open').mockImplementation(() => null);
    const onInteract = vi.fn();
    const { getByText } = render(() => (
      <MessageComponents messageId="m-1" interactionId="i-1" actionRows={[linkRow]} onInteract={onInteract} />
    ));
    fireEvent.click(getByText('Visit'));
    expect(openSpy).toHaveBeenCalledWith('https://example.com', '_blank', 'noreferrer');
    expect(onInteract).not.toHaveBeenCalled();
    openSpy.mockRestore();
  });

  it('renders a select menu with its placeholder as aria-label', () => {
    const selectRow: ActionRow = {
      type: 'action_row',
      components: [
        {
          type: 'select_menu',
          menu: {
            customId: 's-1',
            placeholder: 'Pick one',
            options: [{ label: 'A', value: 'a' }, { label: 'B', value: 'b' }],
          },
        },
      ],
    };
    const { getByLabelText } = render(() => (
      <MessageComponents messageId="m-1" interactionId="i-1" actionRows={[selectRow]} />
    ));
    expect(getByLabelText('Pick one')).toBeInTheDocument();
  });
});
