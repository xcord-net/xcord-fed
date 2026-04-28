import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';

// StatusPicker is heavy (touches presence/auth/signalR stores); stub it out.
vi.mock('../StatusPicker', () => ({
  default: () => <div data-testid="mock-status-picker" />,
}));

// Import after mocks.
import UserStatusBar from './UserStatusBar';

function makeProps(overrides: Partial<Parameters<typeof UserStatusBar>[0]> = {}) {
  return {
    profile: { username: 'alice', displayName: 'Alice', avatarUrl: undefined },
    isAdmin: false,
    version: '1.2.3',
    onOpenSettings: vi.fn(),
    onLogout: vi.fn(),
    ...overrides,
  };
}

describe('UserStatusBar', () => {
  it('renders without crashing and shows the profile display name', () => {
    const props = makeProps();
    const { getByTestId, getAllByText } = render(() => <UserStatusBar {...props} />);
    expect(getByTestId('nav-user-avatar')).toBeInTheDocument();
    // Display name appears at least once (username span + collapsed tooltip).
    expect(getAllByText('Alice').length).toBeGreaterThan(0);
  });

  it('renders the version badge for a valid version', () => {
    const props = makeProps({ version: '1.2.3' });
    const { getByTestId } = render(() => <UserStatusBar {...props} />);
    expect(getByTestId('version-badge')).toHaveTextContent('v1.2.3');
  });

  it('hides the version badge for placeholder versions like 0.0.0-dev', () => {
    const props = makeProps({ version: '0.0.0-dev' });
    const { queryByTestId } = render(() => <UserStatusBar {...props} />);
    expect(queryByTestId('version-badge')).toBeNull();
  });

  it('renders no user bar when profile is null', () => {
    const props = makeProps({ profile: null });
    const { queryByTestId } = render(() => <UserStatusBar {...props} />);
    expect(queryByTestId('nav-user-avatar')).toBeNull();
  });

  it('falls back to the username initial when no avatarUrl is provided', () => {
    const props = makeProps({
      profile: { username: 'bob', displayName: undefined, avatarUrl: undefined },
    });
    const { getByTestId } = render(() => <UserStatusBar {...props} />);
    const avatar = getByTestId('nav-user-avatar');
    expect(avatar.textContent).toContain('B');
  });

  it('invokes onOpenSettings when the settings button is clicked', () => {
    const onOpenSettings = vi.fn();
    const props = makeProps({ onOpenSettings });
    const { getByTestId } = render(() => <UserStatusBar {...props} />);
    fireEvent.click(getByTestId('nav-user-settings-button'));
    expect(onOpenSettings).toHaveBeenCalledOnce();
  });

  it('invokes onLogout when the logout button is clicked', () => {
    const onLogout = vi.fn();
    const props = makeProps({ onLogout });
    const { getByTestId } = render(() => <UserStatusBar {...props} />);
    fireEvent.click(getByTestId('nav-logout-button'));
    expect(onLogout).toHaveBeenCalledOnce();
  });
});
