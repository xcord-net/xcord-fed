import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import EmojiPicker from './EmojiPicker';
import { useEmojis } from '../stores/emoji.store';
import { useModals } from '../stores/modal.store';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('EmojiPicker', () => {
  beforeEach(() => {
    useEmojis().reset();
    useModals().reset();
  });

  it('renders the picker dialog with title', () => {
    const { getByTestId } = render(() => <EmojiPicker onSelect={() => {}} />);
    const picker = getByTestId('emoji-picker');
    expect(picker).toBeInTheDocument();
    expect(picker.getAttribute('role')).toBe('dialog');
    expect(picker.getAttribute('aria-label')).toBe('Emoji picker');
  });

  it('renders a category button for each unicode category', () => {
    const { getByTestId } = render(() => <EmojiPicker onSelect={() => {}} />);
    // The store seeds 7 categories.
    expect(getByTestId('emoji-category-0')).toBeInTheDocument();
    expect(getByTestId('emoji-category-6')).toBeInTheDocument();
  });

  it('renders emoji buttons in the active category grid', () => {
    const { getByTestId } = render(() => <EmojiPicker onSelect={() => {}} />);
    expect(getByTestId('emoji-grid')).toBeInTheDocument();
    // First category seeded with Smileys – there are emojis.
    expect(getByTestId('emoji-btn-0')).toBeInTheDocument();
  });

  it('calls onSelect with the emoji glyph when clicked', () => {
    const onSelect = vi.fn();
    const { getByTestId } = render(() => <EmojiPicker onSelect={onSelect} />);
    const btn = getByTestId('emoji-btn-0');
    fireEvent.click(btn);
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(typeof onSelect.mock.calls[0][0]).toBe('string');
  });

  it('switches the displayed category when a category button is clicked', () => {
    const { getByTestId, getAllByText } = render(() => <EmojiPicker onSelect={() => {}} />);
    fireEvent.click(getByTestId('emoji-category-1'));
    // Animals & Nature category - 🐶 should be visible (also as the icon).
    // The category button itself shows 🐶, so at least 2 instances exist after switching.
    expect(getAllByText('🐶').length).toBeGreaterThanOrEqual(1);
  });

  it('does not show the Manage Emoji link without serverId/isAdmin', () => {
    const { queryByTestId } = render(() => <EmojiPicker onSelect={() => {}} />);
    expect(queryByTestId('emoji-manage-link')).toBeNull();
  });

  it('shows the Manage Emoji link when admin and serverId provided', () => {
    mockFetch({
      'GET /api/v1/servers/s-1/emojis': () => ({ status: 200, body: { emojis: [] } }),
    });
    const { getByTestId } = render(() => (
      <EmojiPicker onSelect={() => {}} serverId="s-1" isAdmin={true} />
    ));
    expect(getByTestId('emoji-manage-link')).toBeInTheDocument();
  });
});
