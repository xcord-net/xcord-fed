import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import Strip from './Strip';
import { HOME_TAB_ID, type DeckTab } from '../../stores/deck.store';

const tab = (id: string, name = `chan-${id}`): DeckTab => ({
  id,
  kind: 'channel',
  name,
  serverId: 's1',
  conversationId: `conv-${id}`,
});

function renderStrip(overrides: Partial<Parameters<typeof Strip>[0]> = {}) {
  const props = {
    pinned: [] as DeckTab[],
    ghosts: [] as DeckTab[],
    activeTabId: HOME_TAB_ID,
    homeUnread: 0,
    unreadFor: () => 0,
    accentFor: () => '#d4703f',
    onSelect: vi.fn(),
    onSelectHome: vi.fn(),
    onClose: vi.fn(),
    onPromoteGhost: vi.fn(),
    onOpenSwitchboard: vi.fn(),
    ...overrides,
  };
  return { props, ...render(() => <Strip {...props} />) };
}

describe('Deck Strip', () => {
  it('always renders Home as the leftmost tab', () => {
    const { getByTestId, container } = renderStrip({ pinned: [tab('1')] });
    expect(getByTestId('deck-tab-home')).toBeInTheDocument();
    const tabs = container.querySelectorAll('[data-testid="deck-tab-home"], [data-testid="deck-tab"]');
    expect(tabs[0].getAttribute('data-testid')).toBe('deck-tab-home');
  });

  it('renders no rails, only the one strip', () => {
    const { container } = renderStrip();
    expect(container.querySelectorAll('[data-testid="deck-strip"]')).toHaveLength(1);
  });

  it('shows the aggregate unread badge on Home', () => {
    const { getByTestId } = renderStrip({ homeUnread: 7 });
    expect(getByTestId('deck-home-unread').textContent).toBe('7');
  });

  it('caps the Home badge display at 99+', () => {
    const { getByTestId } = renderStrip({ homeUnread: 250 });
    expect(getByTestId('deck-home-unread').textContent).toBe('99+');
  });

  it('omits the Home badge when everything is read', () => {
    const { queryByTestId } = renderStrip({ homeUnread: 0 });
    expect(queryByTestId('deck-home-unread')).toBeNull();
  });

  it('marks the active tab as selected for assistive tech', () => {
    const { container } = renderStrip({ pinned: [tab('1'), tab('2')], activeTabId: '2' });
    const tabs = [...container.querySelectorAll('[data-testid="deck-tab"]')];
    expect(tabs.map((t) => t.getAttribute('aria-selected'))).toEqual(['false', 'true']);
  });

  it('selects a pinned tab on click', () => {
    const { props, getAllByTestId } = renderStrip({ pinned: [tab('1')] });
    fireEvent.click(getAllByTestId('deck-tab-select')[0]);
    expect(props.onSelect).toHaveBeenCalledWith(expect.objectContaining({ id: '1' }));
  });

  it('closes a pinned tab from its own control, without selecting it', () => {
    const { props, getByTestId } = renderStrip({ pinned: [tab('1')] });
    fireEvent.click(getByTestId('deck-tab-close'));
    expect(props.onClose).toHaveBeenCalledWith(expect.objectContaining({ id: '1' }));
    expect(props.onSelect).not.toHaveBeenCalled();
  });

  it('returns to Home when the Home tab is clicked', () => {
    const { props, getByTestId } = renderStrip({ activeTabId: '1', pinned: [tab('1')] });
    fireEvent.click(getByTestId('deck-tab-home'));
    expect(props.onSelectHome).toHaveBeenCalled();
  });

  it('renders ghosts distinctly from pinned tabs', () => {
    const { getAllByTestId, queryAllByTestId } = renderStrip({
      pinned: [tab('1')],
      ghosts: [tab('9', 'patch-notes')],
    });
    expect(getAllByTestId('deck-ghost-tab')).toHaveLength(1);
    expect(queryAllByTestId('deck-tab')).toHaveLength(1);
  });

  it('a ghost has no close control, because it was never committed to', () => {
    const { queryByTestId } = renderStrip({ ghosts: [tab('9')] });
    expect(queryByTestId('deck-tab-close')).toBeNull();
  });

  it('clicking a ghost promotes it rather than merely selecting it', () => {
    const { props, getByTestId } = renderStrip({ ghosts: [tab('9')] });
    fireEvent.click(getByTestId('deck-ghost-tab'));
    expect(props.onPromoteGhost).toHaveBeenCalledWith(expect.objectContaining({ id: '9' }));
    expect(props.onSelect).not.toHaveBeenCalled();
  });

  it('opens the switchboard from both the + and the shortcut hint', () => {
    const { props, getByTestId } = renderStrip();
    fireEvent.click(getByTestId('deck-add-tab'));
    fireEvent.click(getByTestId('deck-switchboard-hint'));
    expect(props.onOpenSwitchboard).toHaveBeenCalledTimes(2);
  });

  it("the colour dot opens the tab's community, not the conversation", () => {
    const onOpenCommunity = vi.fn();
    const { props, getAllByTestId } = renderStrip({ pinned: [tab('1')], onOpenCommunity });
    fireEvent.click(getAllByTestId('deck-tab-community')[0]);
    expect(onOpenCommunity).toHaveBeenCalledWith(expect.objectContaining({ id: '1' }));
    expect(props.onSelect).not.toHaveBeenCalled();
  });

  it('shows per-tab unread counts', () => {
    const { getAllByTestId } = renderStrip({
      pinned: [tab('1')],
      unreadFor: (t: DeckTab) => (t.id === '1' ? 12 : 0),
    });
    expect(getAllByTestId('deck-tab')[0].textContent).toContain('12');
  });

  it('animates the waveform only when voice is actually live', () => {
    const quiet = renderStrip({ liveVoice: false });
    expect(quiet.getByTestId('deck-waveform-mark').getAttribute('data-live')).toBe('false');
    quiet.unmount();

    const live = renderStrip({ liveVoice: true });
    expect(live.getByTestId('deck-waveform-mark').getAttribute('data-live')).toBe('true');
  });
});

describe('Deck Strip settings tabs', () => {
  const settings = (id: string, name: string): DeckTab => ({
    id,
    kind: 'settings',
    name,
    settingsScope: 'user',
  });

  it('renders settings tabs after the pinned set', () => {
    const { getAllByTestId } = renderStrip({
      pinned: [tab('1')],
      ephemeral: [settings('settings:user', 'Settings')],
    });
    expect(getAllByTestId('deck-utility-tab')).toHaveLength(1);
  });

  it('shows no settings tab when none is open', () => {
    const { queryAllByTestId } = renderStrip({ pinned: [tab('1')] });
    expect(queryAllByTestId('deck-utility-tab')).toHaveLength(0);
  });

  it('selects a settings tab', () => {
    const s = settings('settings:user', 'Settings');
    const { props, getAllByTestId } = renderStrip({ ephemeral: [s] });
    fireEvent.click(getAllByTestId('deck-tab-select')[0]);
    expect(props.onSelect).toHaveBeenCalledWith(s);
  });

  it('closes a settings tab', () => {
    const s = settings('settings:user', 'Settings');
    const { props, getAllByTestId } = renderStrip({ ephemeral: [s] });
    fireEvent.click(getAllByTestId('deck-tab-close')[0]);
    expect(props.onClose).toHaveBeenCalledWith(s);
  });

  it('marks the settings tab selected when it is active', () => {
    const { getByTestId } = renderStrip({
      ephemeral: [settings('settings:user', 'Settings')],
      activeTabId: 'settings:user',
    });
    expect(getByTestId('deck-utility-tab').getAttribute('aria-selected')).toBe('true');
  });

  // A settings screen has nothing to be behind on, so it never carries the
  // community dot that would let you open a community from it.
  it('gives a settings tab no community dot', () => {
    const { getByTestId, queryAllByTestId } = renderStrip({
      ephemeral: [settings('settings:user', 'Settings')],
    });
    expect(getByTestId('deck-utility-tab')).toBeInTheDocument();
    expect(queryAllByTestId('deck-tab-community')).toHaveLength(0);
  });
});
