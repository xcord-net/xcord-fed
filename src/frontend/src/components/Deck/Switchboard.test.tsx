import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import Switchboard, { filterEntries, groupEntries, type SwitchboardEntry } from './Switchboard';
import type { DeckTab } from '../../stores/deck.store';

const tab = (id: string, name: string, kind: DeckTab['kind'] = 'channel'): DeckTab => ({
  id,
  kind,
  name,
  serverId: kind === 'channel' ? 's1' : undefined,
  conversationId: `conv-${id}`,
});

const entries: SwitchboardEntry[] = [
  { tab: tab('1', 'general'), group: 'The Foundry', kind: 'Text' },
  { tab: tab('2', 'forge-floor'), group: 'The Foundry', kind: 'Voice', unread: 3 },
  { tab: tab('3', 'builds'), group: 'Night Shift', kind: 'Text' },
  { tab: tab('4', 'mara', 'dm'), group: 'Direct messages' },
];

function renderBoard(overrides: Partial<Parameters<typeof Switchboard>[0]> = {}) {
  const props = {
    open: true,
    entries,
    isPinned: () => false,
    onOpen: vi.fn(),
    onTogglePin: vi.fn(),
    onCreateServer: vi.fn(),
    onClose: vi.fn(),
    ...overrides,
  };
  return { props, ...render(() => <Switchboard {...props} />) };
}

describe('filterEntries', () => {
  it('returns everything for an empty query', () => {
    expect(filterEntries(entries, '   ')).toHaveLength(4);
  });

  it('matches on conversation name, case-insensitively', () => {
    expect(filterEntries(entries, 'FORGE').map((e) => e.tab.name)).toEqual(['forge-floor']);
  });

  it('matches on community name, so you can find a whole community', () => {
    expect(filterEntries(entries, 'night').map((e) => e.tab.name)).toEqual(['builds']);
  });

  it('returns nothing when there is no match', () => {
    expect(filterEntries(entries, 'zzz')).toEqual([]);
  });
});

describe('groupEntries', () => {
  it('groups by community and preserves first-seen order', () => {
    expect(groupEntries(entries).map(([g]) => g)).toEqual([
      'The Foundry',
      'Night Shift',
      'Direct messages',
    ]);
  });

  it('keeps every entry in its group', () => {
    const grouped = groupEntries(entries);
    expect(grouped[0][1].map((e) => e.tab.name)).toEqual(['general', 'forge-floor']);
  });
});

describe('Switchboard', () => {
  it('renders nothing when closed', () => {
    const { queryByTestId } = renderBoard({ open: false });
    expect(queryByTestId('switchboard')).toBeNull();
  });

  it('lists every conversation grouped by community', () => {
    const { getAllByTestId } = renderBoard();
    expect(getAllByTestId('switchboard-result')).toHaveLength(4);
  });

  it('narrows the list as you type', () => {
    const { getByTestId, getAllByTestId } = renderBoard();
    fireEvent.input(getByTestId('switchboard-input'), { target: { value: 'builds' } });
    expect(getAllByTestId('switchboard-result')).toHaveLength(1);
  });

  it('explains an empty result rather than showing a blank panel', () => {
    const { getByTestId } = renderBoard();
    fireEvent.input(getByTestId('switchboard-input'), { target: { value: 'zzz' } });
    expect(getByTestId('switchboard-empty')).toBeInTheDocument();
  });

  it('jumps to a conversation on click and closes', () => {
    const { props, getAllByTestId } = renderBoard();
    fireEvent.click(getAllByTestId('switchboard-result-open')[0]);
    expect(props.onOpen).toHaveBeenCalledWith(expect.objectContaining({ name: 'general' }));
    expect(props.onClose).toHaveBeenCalled();
  });

  it('pins straight from a result row without jumping to it', () => {
    const { props, getAllByTestId } = renderBoard();
    fireEvent.click(getAllByTestId('switchboard-pin')[0]);
    expect(props.onTogglePin).toHaveBeenCalledWith(expect.objectContaining({ name: 'general' }));
    expect(props.onOpen).not.toHaveBeenCalled();
  });

  it('reflects pinned state on the row control', () => {
    const { getAllByTestId } = renderBoard({ isPinned: (id: string) => id === '1' });
    expect(getAllByTestId('switchboard-pin')[0].getAttribute('aria-pressed')).toBe('true');
    expect(getAllByTestId('switchboard-pin')[1].getAttribute('aria-pressed')).toBe('false');
  });

  it('enter jumps to the cursor row', () => {
    const { props } = renderBoard();
    fireEvent.keyDown(document, { key: 'ArrowDown' });
    fireEvent.keyDown(document, { key: 'Enter' });
    expect(props.onOpen).toHaveBeenCalledWith(expect.objectContaining({ name: 'forge-floor' }));
  });

  it('arrow keys do not run off either end of the list', () => {
    const onOpen = vi.fn();
    renderBoard({ onOpen });
    for (let i = 0; i < 10; i++) fireEvent.keyDown(document, { key: 'ArrowDown' });
    fireEvent.keyDown(document, { key: 'Enter' });
    expect(onOpen).toHaveBeenCalledWith(expect.objectContaining({ name: 'mara' }));

    onOpen.mockClear();
    for (let i = 0; i < 20; i++) fireEvent.keyDown(document, { key: 'ArrowUp' });
    fireEvent.keyDown(document, { key: 'Enter' });
    expect(onOpen).toHaveBeenCalledWith(expect.objectContaining({ name: 'general' }));
  });

  it('keeps the cursor valid when the query shrinks the list under it', () => {
    const { props, getByTestId } = renderBoard();
    fireEvent.keyDown(document, { key: 'ArrowDown' });
    fireEvent.keyDown(document, { key: 'ArrowDown' });
    fireEvent.keyDown(document, { key: 'ArrowDown' });
    fireEvent.input(getByTestId('switchboard-input'), { target: { value: 'general' } });
    fireEvent.keyDown(document, { key: 'Enter' });
    expect(props.onOpen).toHaveBeenCalledWith(expect.objectContaining({ name: 'general' }));
  });

  it('escape closes it', () => {
    const { props } = renderBoard();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(props.onClose).toHaveBeenCalled();
  });

  it('clicking the backdrop closes it', () => {
    const { props, getByTestId } = renderBoard();
    fireEvent.click(getByTestId('switchboard-backdrop'));
    expect(props.onClose).toHaveBeenCalled();
  });

  it('offers creating a community, the affordance the server rail used to own', () => {
    const { props, getByTestId } = renderBoard();
    fireEvent.click(getByTestId('sidebar-create-server-button'));
    expect(props.onCreateServer).toHaveBeenCalled();
  });

  it('ignores keys entirely while closed', () => {
    const { props } = renderBoard({ open: false });
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(props.onClose).not.toHaveBeenCalled();
  });
});
