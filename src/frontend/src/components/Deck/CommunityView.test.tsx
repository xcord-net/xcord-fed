import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import CommunityView, {
  formatPrice,
  formatCounts,
  type CommunityRoom,
  type CommunityPerson,
  type CommunityTier,
} from './CommunityView';
import type { DeckTab } from '../../stores/deck.store';

const tab = (id: string, name: string): DeckTab => ({
  id,
  kind: 'channel',
  name,
  serverId: 's1',
  conversationId: `conv-${id}`,
});

const rooms: CommunityRoom[] = [
  { tab: tab('1', 'general'), kind: 'Text', unread: 0, liveVoiceCount: 0 },
  { tab: tab('2', 'forge-floor'), kind: 'Voice', unread: 0, liveVoiceCount: 4 },
  { tab: tab('3', 'members-lounge'), kind: 'Text', unread: 7, liveVoiceCount: 0, gatedBy: 'Patrons' },
];

const people: CommunityPerson[] = [
  { userId: 'u1', name: 'mara', tierEmblem: '★', tierName: 'Patrons' },
  { userId: 'u2', name: 'ollie' },
];

const tiers: CommunityTier[] = [
  { id: 't1', name: 'Supporter', priceMonthly: 500, currency: 'usd', description: 'Thanks.' },
  { id: 't2', name: 'Patrons', priceMonthly: 1500, currency: 'usd', isCurrent: true },
];

function renderView(overrides: Partial<Parameters<typeof CommunityView>[0]> = {}) {
  const props = {
    name: 'The Foundry',
    domain: 'foundry.xcord.net',
    accent: '#d4703f',
    memberCount: 128,
    voiceCount: 4,
    rooms,
    people,
    peopleTotal: 128,
    tiers,
    canManage: true,
    isPinned: () => false,
    onOpenRoom: vi.fn(),
    onTogglePin: vi.fn(),
    onManage: vi.fn(),
    onSubscribe: vi.fn(),
    ...overrides,
  };
  return { props, ...render(() => <CommunityView {...props} />) };
}

describe('formatPrice', () => {
  it('renders cents as dollars with the currency', () => {
    expect(formatPrice(500, 'usd')).toBe('$5.00 USD/mo');
    expect(formatPrice(1500, 'usd')).toBe('$15.00 USD/mo');
  });
});

describe('formatCounts', () => {
  it('counts members and voice', () => {
    expect(formatCounts(128, 4)).toBe('128 members · 4 in voice');
  });

  it('omits voice when nobody is in it', () => {
    expect(formatCounts(128, 0)).toBe('128 members');
  });

  it('uses the singular for one member', () => {
    expect(formatCounts(1, 0)).toBe('1 member');
  });
});

describe('CommunityView', () => {
  it('leads with the community identity', () => {
    const { getByTestId } = renderView();
    expect(getByTestId('community-name').textContent).toBe('The Foundry');
    expect(getByTestId('community-domain').textContent).toBe('foundry.xcord.net');
    expect(getByTestId('community-counts').textContent).toBe('128 members · 4 in voice');
  });

  it('lists every room', () => {
    const { getAllByTestId } = renderView();
    expect(getAllByTestId('community-room')).toHaveLength(3);
  });

  it('opens a room', () => {
    const { props, getAllByTestId } = renderView();
    fireEvent.click(getAllByTestId('community-room-open')[0]);
    expect(props.onOpenRoom).toHaveBeenCalledWith(expect.objectContaining({ name: 'general' }));
  });

  it('pins a room without opening it', () => {
    const { props, getAllByTestId } = renderView();
    fireEvent.click(getAllByTestId('community-room-pin')[0]);
    expect(props.onTogglePin).toHaveBeenCalled();
    expect(props.onOpenRoom).not.toHaveBeenCalled();
  });

  it('reflects pinned state on the room control', () => {
    const { getAllByTestId } = renderView({ isPinned: (id: string) => id === '1' });
    expect(getAllByTestId('community-room-pin')[0].getAttribute('aria-pressed')).toBe('true');
    expect(getAllByTestId('community-room-pin')[1].getAttribute('aria-pressed')).toBe('false');
  });

  it('labels a gated room so the restriction is visible before joining', () => {
    const { getAllByTestId } = renderView();
    const gates = getAllByTestId('community-room-gate');
    expect(gates).toHaveLength(1);
    expect(gates[0].textContent).toBe('Patrons');
  });

  it('marks a room that is live right now', () => {
    const { getAllByTestId } = renderView();
    expect(getAllByTestId('community-room-live')[0].textContent).toContain('4');
  });

  it('says so when a community has no rooms yet', () => {
    const { getByTestId } = renderView({ rooms: [] });
    expect(getByTestId('community-rooms-empty')).toBeInTheDocument();
  });

  it('previews people and says how many more there are', () => {
    const { getAllByTestId, getByTestId } = renderView();
    expect(getAllByTestId('community-person')).toHaveLength(2);
    expect(getByTestId('community-people-more').textContent).toBe('+126 more');
  });

  it('omits the overflow note when everyone is shown', () => {
    const { queryByTestId } = renderView({ peopleTotal: 2 });
    expect(queryByTestId('community-people-more')).toBeNull();
  });

  it('shows a tier emblem only for members who hold one', () => {
    const { getAllByTestId } = renderView();
    const emblems = getAllByTestId('community-person-emblem');
    expect(emblems).toHaveLength(1);
    expect(emblems[0].getAttribute('title')).toBe('Patrons');
  });

  it('lists tiers with prices', () => {
    const { getAllByTestId } = renderView();
    const cards = getAllByTestId('community-tier');
    expect(cards).toHaveLength(2);
    expect(cards[0].textContent).toContain('$5.00 USD/mo');
  });

  it('marks the pass the viewer already holds instead of selling it again', () => {
    const { getByTestId, getAllByTestId } = renderView();
    expect(getByTestId('community-tier-current').textContent).toBe('Your pass');
    // Only the tier they do not hold offers a CTA.
    expect(getAllByTestId('community-tier-subscribe')).toHaveLength(1);
  });

  it('subscribes to a tier', () => {
    const { props, getAllByTestId } = renderView();
    fireEvent.click(getAllByTestId('community-tier-subscribe')[0]);
    expect(props.onSubscribe).toHaveBeenCalledWith('t1');
  });

  it('hides the membership section entirely when there are no tiers', () => {
    const { queryAllByTestId } = renderView({ tiers: [] });
    expect(queryAllByTestId('community-tier')).toHaveLength(0);
  });

  it('offers Manage only to someone who can manage', () => {
    const canManage = renderView({ canManage: true });
    expect(canManage.getByTestId('community-manage')).toBeInTheDocument();
    canManage.unmount();

    const cannot = renderView({ canManage: false });
    expect(cannot.queryByTestId('community-manage')).toBeNull();
  });

  it('opens management', () => {
    const { props, getByTestId } = renderView();
    fireEvent.click(getByTestId('community-manage'));
    expect(props.onManage).toHaveBeenCalled();
  });
});
