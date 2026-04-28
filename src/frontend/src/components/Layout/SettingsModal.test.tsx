import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';

// Stub each tab body so the test focuses on tab switching + modal open/close.
vi.mock('../BlockList', () => ({
  default: () => <div data-testid="mock-block-list" />,
}));
vi.mock('../NotificationSettings', () => ({
  default: () => <div data-testid="mock-notification-settings" />,
}));
vi.mock('../UserNotes', () => ({
  default: () => <div data-testid="mock-user-notes" />,
}));
vi.mock('../UserProfileEditor', () => ({
  default: () => <div data-testid="mock-user-profile-editor" />,
}));

import SettingsModal from './SettingsModal';
import { useModals } from '../../stores/modal.store';

describe('SettingsModal', () => {
  beforeEach(() => {
    useModals().reset();
  });

  it('renders nothing when modals.showSettings is null (modal closed)', () => {
    const { queryByTestId } = render(() => <SettingsModal />);
    expect(queryByTestId('user-settings-modal')).toBeNull();
  });

  it('renders the modal with the profile tab body when opened to "profile"', () => {
    useModals().openSettings('profile');
    const { getByTestId } = render(() => <SettingsModal />);
    expect(getByTestId('user-settings-modal')).toBeInTheDocument();
    expect(getByTestId('mock-user-profile-editor')).toBeInTheDocument();
  });

  it('switches to the notifications tab body when the notifications tab is clicked', () => {
    useModals().openSettings('profile');
    const { getByTestId, queryByTestId } = render(() => <SettingsModal />);
    expect(queryByTestId('mock-notification-settings')).toBeNull();
    fireEvent.click(getByTestId('settings-tab-notifications'));
    expect(getByTestId('mock-notification-settings')).toBeInTheDocument();
    expect(queryByTestId('mock-user-profile-editor')).toBeNull();
  });

  it('renders the BlockList body when modal state is "blocks"', () => {
    useModals().openSettings('blocks');
    const { getByTestId, queryByTestId } = render(() => <SettingsModal />);
    expect(getByTestId('mock-block-list')).toBeInTheDocument();
    expect(queryByTestId('mock-user-profile-editor')).toBeNull();
  });

  it('renders the UserNotes body when modal state is "notes"', () => {
    useModals().openSettings('notes');
    const { getByTestId } = render(() => <SettingsModal />);
    expect(getByTestId('mock-user-notes')).toBeInTheDocument();
  });

  it('marks the active tab with the active class', () => {
    useModals().openSettings('notifications');
    const { getByTestId } = render(() => <SettingsModal />);
    const activeTab = getByTestId('settings-tab-notifications');
    const inactiveTab = getByTestId('settings-tab-profile');
    // The active tab carries an additional class beyond the base settingsTab class.
    expect(activeTab.className.split(' ').length).toBeGreaterThan(
      inactiveTab.className.split(' ').length,
    );
  });
});
