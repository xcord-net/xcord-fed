import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import ServerHeader from './ServerHeader';

function makeProps(overrides: Partial<Parameters<typeof ServerHeader>[0]> = {}) {
  return {
    server: { id: 's-1', name: 'My Server' },
    selectedServerId: 's-1' as string | null,
    showServerMenu: false,
    onMenuOpen: vi.fn(),
    onMenuClose: vi.fn(),
    onNavigateToServer: vi.fn(),
    onCreateChannel: vi.fn(),
    onOpenServerSettings: vi.fn(),
    onOpenInvite: vi.fn(),
    onToggleEvents: vi.fn(),
    onOpenGroups: vi.fn(),
    onLeaveServer: vi.fn(),
    ...overrides,
  };
}

describe('ServerHeader', () => {
  it('offers a way into groups and permissions', () => {
    // Regression: toggleGroupManager() existed in the modal store but nothing
    // called it, so the roles and permissions editor could not be opened by any
    // sequence of clicks. An admin surface with no entry point is not a feature.
    const onOpenGroups = vi.fn();
    const props = makeProps({ showServerMenu: true, onOpenGroups });
    const { getByTestId } = render(() => <ServerHeader {...props} />);
    fireEvent.click(getByTestId('server-menu-groups'));
    expect(onOpenGroups).toHaveBeenCalled();
  });

  it('renders without crashing and shows the server name', () => {
    const props = makeProps();
    const { getByTestId } = render(() => <ServerHeader {...props} />);
    expect(getByTestId('server-name-heading')).toHaveTextContent('My Server');
  });

  it('renders icon initials when no iconUrl is provided', () => {
    const props = makeProps({ server: { id: 's-1', name: 'Foo Bar Baz' } });
    const { getByTestId } = render(() => <ServerHeader {...props} />);
    const button = getByTestId('nav-server-icon');
    expect(button).toHaveAttribute('aria-label', 'Foo Bar Baz');
    // initials of three-word name = first letters of each word
    expect(button.textContent).toBe('FBB');
  });

  it('renders an <img> when iconUrl is provided', () => {
    const props = makeProps({
      server: { id: 's-1', name: 'My Server', iconUrl: 'https://example.com/icon.png' },
    });
    const { getByTestId } = render(() => <ServerHeader {...props} />);
    const img = getByTestId('nav-server-icon').querySelector('img');
    expect(img).not.toBeNull();
    expect(img!.getAttribute('src')).toBe('https://example.com/icon.png');
  });

  it('invokes onNavigateToServer with the server id when icon is clicked', () => {
    const onNavigateToServer = vi.fn();
    const props = makeProps({ onNavigateToServer });
    const { getByTestId } = render(() => <ServerHeader {...props} />);
    fireEvent.click(getByTestId('nav-server-icon'));
    expect(onNavigateToServer).toHaveBeenCalledWith('s-1');
  });

  it('invokes onCreateChannel when the plus button is clicked', () => {
    const onCreateChannel = vi.fn();
    const props = makeProps({ onCreateChannel });
    const { getByTestId } = render(() => <ServerHeader {...props} />);
    fireEvent.click(getByTestId('create-channel-button'));
    expect(onCreateChannel).toHaveBeenCalledOnce();
  });

  it('hides the menu trigger when there is no selectedServerId', () => {
    const props = makeProps({ selectedServerId: null });
    const { queryByTestId } = render(() => <ServerHeader {...props} />);
    expect(queryByTestId('server-menu-trigger')).toBeNull();
  });

  it('invokes onMenuOpen when the menu trigger is clicked', () => {
    const onMenuOpen = vi.fn();
    const props = makeProps({ onMenuOpen });
    const { getByTestId } = render(() => <ServerHeader {...props} />);
    fireEvent.click(getByTestId('server-menu-trigger'));
    expect(onMenuOpen).toHaveBeenCalledOnce();
  });
});
