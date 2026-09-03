import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import HomeTab, { summarise, type UnreadCard, type AmbientCard } from './HomeTab';
import type { DeckTab } from '../../stores/deck.store';

const tab = (id: string, name: string): DeckTab => ({
  id,
  kind: 'channel',
  name,
  serverId: 's1',
  conversationId: `conv-${id}`,
});

const unreadCards: UnreadCard[] = [
  { tab: tab('1', 'builds'), community: 'Night Shift', unread: 12 },
];

const ambientCards: AmbientCard[] = [
  {
    tab: tab('2', 'forge-floor'),
    community: 'The Foundry',
    label: 'live now',
    detail: 'mara, ollie and 3 others are in voice',
    kind: 'live',
  },
];

function renderHome(overrides: Partial<Parameters<typeof HomeTab>[0]> = {}) {
  const props = {
    unreadCards,
    ambientCards,
    onOpen: vi.fn(),
    onPin: vi.fn(),
    onMarkRead: vi.fn(),
    ...overrides,
  };
  return { props, ...render(() => <HomeTab {...props} />) };
}

describe('summarise', () => {
  it('counts both kinds', () => {
    expect(summarise(3, 2)).toBe('3 conversations · 2 things happening in your communities');
  });

  it('uses singular forms', () => {
    expect(summarise(1, 1)).toBe('1 conversation · 1 thing happening in your communities');
  });

  it('omits a zero section rather than saying "0"', () => {
    expect(summarise(2, 0)).toBe('2 conversations');
    expect(summarise(0, 2)).toBe('2 things happening in your communities');
  });
});

describe('HomeTab', () => {
  it('renders both sections', () => {
    const { getAllByTestId } = renderHome();
    expect(getAllByTestId('home-unread-card')).toHaveLength(1);
    expect(getAllByTestId('home-ambient-card')).toHaveLength(1);
  });

  it('summarises what is waiting', () => {
    const { getByTestId } = renderHome();
    expect(getByTestId('deck-home-summary').textContent).toContain('1 conversation');
  });

  it('opens a conversation from an unread card', () => {
    const { props, getByTestId } = renderHome();
    fireEvent.click(getByTestId('home-card-open'));
    expect(props.onOpen).toHaveBeenCalledWith(expect.objectContaining({ name: 'builds' }));
  });

  it('pins from an unread card without opening it', () => {
    const { props, getByTestId } = renderHome();
    fireEvent.click(getByTestId('home-card-pin'));
    expect(props.onPin).toHaveBeenCalled();
    expect(props.onOpen).not.toHaveBeenCalled();
  });

  it('marks read without opening', () => {
    const { props, getByTestId } = renderHome();
    fireEvent.click(getByTestId('home-card-mark-read'));
    expect(props.onMarkRead).toHaveBeenCalledWith(expect.objectContaining({ name: 'builds' }));
    expect(props.onOpen).not.toHaveBeenCalled();
  });

  it('opens an ambient item, so live voice is one click away', () => {
    const { props, getByTestId } = renderHome();
    fireEvent.click(getByTestId('home-ambient-open'));
    expect(props.onOpen).toHaveBeenCalledWith(expect.objectContaining({ name: 'forge-floor' }));
  });

  it('shows the unread count on the card', () => {
    const { getByTestId } = renderHome();
    expect(getByTestId('home-unread-card').textContent).toContain('12 new');
  });

  it('hides the unread section entirely when nothing is unread', () => {
    const { queryByTestId } = renderHome({ unreadCards: [] });
    expect(queryByTestId('home-unread-card')).toBeNull();
    expect(queryByTestId('home-ambient-card')).not.toBeNull();
  });

  it('is never a blank screen: an empty deck still gets direction', () => {
    const { getByTestId } = renderHome({ unreadCards: [], ambientCards: [] });
    expect(getByTestId('deck-home-empty').textContent).toContain('⌘K');
  });
});
