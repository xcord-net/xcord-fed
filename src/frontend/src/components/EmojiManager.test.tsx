import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import EmojiManager, { validateEmojiName, deriveEmojiName } from './EmojiManager';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('EmojiManager pure helpers', () => {
  it('validateEmojiName rejects empty / whitespace input', () => {
    expect(validateEmojiName('')).toMatch(/Please enter a name/);
    expect(validateEmojiName('   ')).toMatch(/Please enter a name/);
  });

  it('validateEmojiName rejects names that contain illegal characters', () => {
    expect(validateEmojiName('bad-name!')).toMatch(/2-32 characters/);
  });

  it('validateEmojiName accepts a valid alphanumeric/underscore name', () => {
    expect(validateEmojiName('cool_face_2')).toBeNull();
  });

  it('deriveEmojiName strips extension and lowercases non-alphanumeric chars to underscore', () => {
    expect(deriveEmojiName('Cool Face!.PNG')).toBe('cool_face_');
    expect(deriveEmojiName('happy.gif')).toBe('happy');
  });
});

describe('EmojiManager', () => {
  it('renders the Custom Emojis heading', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/emojis': () => ({ status: 200, body: { emojis: [] } }),
    });
    const { findByText } = render(() => <EmojiManager serverId="s-1" />);
    expect(await findByText('Custom Emojis')).toBeInTheDocument();
  });

  it('shows empty state when no emojis are returned', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/emojis': () => ({ status: 200, body: { emojis: [] } }),
    });
    const { findByTestId } = render(() => <EmojiManager serverId="s-1" />);
    expect(await findByTestId('emoji-list-empty-state')).toBeInTheDocument();
  });

  it('renders an emoji item when emojis are returned', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/emojis': () => ({
        status: 200,
        body: {
          emojis: [
            {
              id: 'em-1',
              name: 'partyhat',
              imageUrl: 'http://example.com/partyhat.png',
              isAnimated: false,
              createdAt: '2025-01-01T00:00:00Z',
            },
          ],
        },
      }),
    });
    const { findByTestId } = render(() => <EmojiManager serverId="s-1" />);
    expect(await findByTestId('emoji-item-partyhat')).toBeInTheDocument();
  });

  it('shows error banner when load fails', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/emojis': () => ({ status: 500, body: { message: 'Boom' } }),
    });
    const { findByText } = render(() => <EmojiManager serverId="s-1" />);
    expect(await findByText(/Failed to load emojis|Boom/)).toBeInTheDocument();
  });

  it('upload form rejects submission without a file selected', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/emojis': () => ({ status: 200, body: { emojis: [] } }),
    });
    const { findByTestId, container } = render(() => <EmojiManager serverId="s-1" />);
    // wait for initial load to settle
    await findByTestId('emoji-list-empty-state');
    const nameInput = await findByTestId('emoji-name-input');
    fireEvent.input(nameInput, { target: { value: 'hello' } });
    const form = container.querySelector(
      '[data-testid="emoji-upload-form"]',
    ) as HTMLFormElement;
    fireEvent.submit(form);
    await waitFor(() => {
      expect(container.textContent).toContain('Please select an image file.');
    });
  });

  it('starts delete confirmation flow when delete button is clicked', async () => {
    mockFetch({
      'GET /api/v1/servers/s-1/emojis': () => ({
        status: 200,
        body: {
          emojis: [
            {
              id: 'em-1',
              name: 'partyhat',
              imageUrl: 'http://example.com/partyhat.png',
              isAnimated: false,
              createdAt: '2025-01-01T00:00:00Z',
            },
          ],
        },
      }),
    });
    const { findByTestId } = render(() => <EmojiManager serverId="s-1" />);
    fireEvent.click(await findByTestId('delete-emoji-button-partyhat'));
    expect(await findByTestId('emoji-delete-confirm-partyhat')).toBeInTheDocument();
  });
});
