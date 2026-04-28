import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  isTabFocused,
  playSound,
  showDesktopNotification,
  handleNewMessageNotification,
} from '../services/notification.service';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeMessage(overrides: Partial<{
  authorId: string;
  conversationId: string;
  content: string;
  authorUsername: string;
}> = {}) {
  return {
    authorId: 'user-other',
    conversationId: 'conv-123',
    content: 'Hello world',
    authorUsername: 'Alice',
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// isTabFocused
// ---------------------------------------------------------------------------

describe('isTabFocused', () => {
  it('returns true after a focus event', () => {
    window.dispatchEvent(new Event('focus'));
    expect(isTabFocused()).toBe(true);
  });

  it('returns false after a blur event', () => {
    window.dispatchEvent(new Event('blur'));
    expect(isTabFocused()).toBe(false);
  });

});

// ---------------------------------------------------------------------------
// playSound
// ---------------------------------------------------------------------------

describe('playSound', () => {
  it('creates an AudioContext and starts an oscillator', () => {
    const stopMock = vi.fn();
    const connectMock = vi.fn();
    const startMock = vi.fn();
    const closeMock = vi.fn();
    const setValueAtTimeMock = vi.fn();
    const exponentialRampMock = vi.fn();

    const oscillatorMock = {
      connect: connectMock,
      start: startMock,
      stop: stopMock,
      type: '' as OscillatorType,
      frequency: { setValueAtTime: setValueAtTimeMock },
      onended: null as (() => void) | null,
    };

    const gainMock = {
      connect: connectMock,
      gain: {
        setValueAtTime: setValueAtTimeMock,
        exponentialRampToValueAtTime: exponentialRampMock,
      },
    };

    // vitest 4 requires `function` (not arrow) for mocks used as constructors.
    const AudioContextMock = vi.fn(function () {
      return {
        createOscillator: vi.fn(() => oscillatorMock),
        createGain: vi.fn(() => gainMock),
        destination: {},
        currentTime: 0,
        close: closeMock,
      };
    });

    vi.stubGlobal('AudioContext', AudioContextMock);

    playSound();

    expect(AudioContextMock).toHaveBeenCalledOnce();
    expect(startMock).toHaveBeenCalledOnce();

    vi.unstubAllGlobals();
  });

  it('falls back to HTMLAudioElement when AudioContext is unavailable', () => {
    // Remove AudioContext so the fallback path is taken.
    const savedAC = (window as unknown as Record<string, unknown>)['AudioContext'];
    const savedWAC = (window as unknown as Record<string, unknown>)['webkitAudioContext'];
    delete (window as unknown as Record<string, unknown>)['AudioContext'];
    delete (window as unknown as Record<string, unknown>)['webkitAudioContext'];

    const playMock = vi.fn().mockResolvedValue(undefined);
    const AudioMock = vi.fn(function () { return { play: playMock, volume: 1 }; });
    vi.stubGlobal('Audio', AudioMock);

    playSound();

    expect(AudioMock).toHaveBeenCalledOnce();
    expect(playMock).toHaveBeenCalledOnce();

    vi.unstubAllGlobals();
    if (savedAC) (window as unknown as Record<string, unknown>)['AudioContext'] = savedAC;
    if (savedWAC) (window as unknown as Record<string, unknown>)['webkitAudioContext'] = savedWAC;
  });
});

// ---------------------------------------------------------------------------
// showDesktopNotification
// ---------------------------------------------------------------------------

describe('showDesktopNotification', () => {
  it('creates a Notification when permission is granted', () => {
    const NotificationMock = vi.fn();
    Object.defineProperty(NotificationMock, 'permission', { value: 'granted', configurable: true });
    vi.stubGlobal('Notification', NotificationMock);

    showDesktopNotification('Test title', 'Test body');

    expect(NotificationMock).toHaveBeenCalledWith('Test title', { body: 'Test body', icon: undefined });

    vi.unstubAllGlobals();
  });

  it('returns null when permission is denied', () => {
    const NotificationMock = vi.fn();
    Object.defineProperty(NotificationMock, 'permission', { value: 'denied', configurable: true });
    vi.stubGlobal('Notification', NotificationMock);

    const result = showDesktopNotification('Test title', 'Test body');

    expect(result).toBeNull();
    expect(NotificationMock).not.toHaveBeenCalled();

    vi.unstubAllGlobals();
  });

  it('returns null when Notification API is unavailable', () => {
    const original = globalThis.Notification;
    // @ts-expect-error intentional removal for test
    delete globalThis.Notification;

    const result = showDesktopNotification('Test title', 'Test body');
    expect(result).toBeNull();

    globalThis.Notification = original;
  });
});

// ---------------------------------------------------------------------------
// handleNewMessageNotification
//
// We test suppression rules by verifying whether AudioContext / Notification
// constructors are called (real side-effects) rather than spying on sibling
// module exports, which is not reliable with ESM live bindings.
// ---------------------------------------------------------------------------

// Hoisted default factory so beforeEach can reseed the mock implementation
// after vi.mocked(...).mockReturnValue() overrides on a per-test basis.
// Must use vi.hoisted because vi.mock's factory runs before module-level vars init.
const { defaultNotificationsStore } = vi.hoisted(() => ({
  defaultNotificationsStore: () => ({
    settings: {
      muteAll: false,
      mutedChannelIds: [],
      mutedServerIds: [],
      userId: '',
      allowDirectMessages: true,
      allowFriendRequests: true,
      showOnlineStatus: true,
      mentionKeywords: [],
    },
    channelOverrides: [],
    isLoading: false,
  }),
}));

vi.mock('../stores/notification.store', () => ({
  useNotifications: vi.fn(defaultNotificationsStore),
}));

describe('handleNewMessageNotification', () => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let audioCtxMock: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let notificationMock: any;

  // Build lightweight AudioContext stub that avoids jsdom errors.
  function makeAudioContextStub() {
    const oscillatorStub = {
      connect: vi.fn(),
      start: vi.fn(),
      stop: vi.fn(),
      type: 'sine' as OscillatorType,
      frequency: { setValueAtTime: vi.fn() },
      onended: null as (() => void) | null,
    };
    const gainStub = {
      connect: vi.fn(),
      gain: {
        setValueAtTime: vi.fn(),
        exponentialRampToValueAtTime: vi.fn(),
      },
    };
    return vi.fn(function () {
      return {
        createOscillator: vi.fn(() => oscillatorStub),
        createGain: vi.fn(() => gainStub),
        destination: {},
        currentTime: 0,
        close: vi.fn(),
      };
    });
  }

  beforeEach(async () => {
    // Re-seed useNotifications default — per-test mockReturnValue overrides
    // (e.g. the muted-channel test) would otherwise persist into later tests.
    const { useNotifications } = await import('../stores/notification.store');
    // Cast through unknown — the test only exercises a narrow subset of the
    // store, and listing every field/method just to satisfy the type would
    // be brittle.
    vi.mocked(useNotifications).mockImplementation(
      defaultNotificationsStore as unknown as typeof useNotifications,
    );

    // Stub AudioContext so playSound() doesn't error in jsdom.
    audioCtxMock = makeAudioContextStub();
    vi.stubGlobal('AudioContext', audioCtxMock);
    // vitest 4's stubGlobal changed how window.* is resolved from constructor
    // mocks; assign to window directly to ensure source code's `window.AudioContext`
    // and `new Notification(...)` see the mocks.
    (window as unknown as Record<string, unknown>).AudioContext = audioCtxMock;

    // Stub Notification with granted permission.
    // vitest 4 requires `function` (not arrow) for mocks called with `new`.
    notificationMock = vi.fn(function () { return {}; });
    Object.defineProperty(notificationMock, 'permission', {
      value: 'granted',
      configurable: true,
      writable: true,
    });
    vi.stubGlobal('Notification', notificationMock);
    (window as unknown as Record<string, unknown>).Notification = notificationMock;

    // Default state: tab NOT focused.
    window.dispatchEvent(new Event('blur'));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    // vitest 4 made restoreAllMocks() also reset module-level vi.mock() factory
    // implementations, which broke per-test useNotifications overrides. Use
    // clearAllMocks (clears call history only) and let beforeEach reseed.
    vi.clearAllMocks();
    // Reset tab focus state.
    window.dispatchEvent(new Event('focus'));
  });

  it('plays sound and shows desktop notification when tab is not focused', () => {
    handleNewMessageNotification(makeMessage(), {
      currentUserId: 'user-me',
      activeConversationId: 'conv-other',
    });

    expect(audioCtxMock).toHaveBeenCalledOnce();
    expect(notificationMock).toHaveBeenCalledOnce();
  });

  it('suppresses notification for own messages', () => {
    handleNewMessageNotification(makeMessage({ authorId: 'user-me' }), {
      currentUserId: 'user-me',
      activeConversationId: null,
    });

    expect(audioCtxMock).not.toHaveBeenCalled();
    expect(notificationMock).not.toHaveBeenCalled();
  });

  it('suppresses notification for muted channels', async () => {
    const { useNotifications } = await import('../stores/notification.store');
    vi.mocked(useNotifications).mockReturnValue({
      settings: {
        muteAll: false,
        mutedChannelIds: ['conv-123'],
        mutedServerIds: [],
        userId: '',
        allowDirectMessages: true,
        allowFriendRequests: true,
        showOnlineStatus: true,
        mentionKeywords: [],
      },
      channelOverrides: [],
      serverOverrides: [],
      isLoading: false,
      loadSettings: vi.fn(),
      updateSettings: vi.fn(),
      muteServer: vi.fn(),
      unmuteServer: vi.fn(),
      muteChannel: vi.fn(),
      unmuteChannel: vi.fn(),
      setChannelOverride: vi.fn(),
      loadChannelOverrides: vi.fn(),
      loadServerOverrides: vi.fn(),
      setServerNotificationLevel: vi.fn(),
      deleteServerNotificationOverride: vi.fn(),
      reset: vi.fn(),
    } as ReturnType<typeof useNotifications>);

    handleNewMessageNotification(makeMessage({ conversationId: 'conv-123' }), {
      currentUserId: 'user-me',
      activeConversationId: null,
    });

    expect(audioCtxMock).not.toHaveBeenCalled();
    expect(notificationMock).not.toHaveBeenCalled();
  });

  it('suppresses notification when tab is focused and same conversation is active', () => {
    window.dispatchEvent(new Event('focus'));

    handleNewMessageNotification(makeMessage({ conversationId: 'conv-123' }), {
      currentUserId: 'user-me',
      activeConversationId: 'conv-123',
    });

    expect(audioCtxMock).not.toHaveBeenCalled();
    expect(notificationMock).not.toHaveBeenCalled();
  });

  it('plays sound but skips desktop notification when tab is focused and different conversation', () => {
    window.dispatchEvent(new Event('focus'));

    handleNewMessageNotification(makeMessage({ conversationId: 'conv-123' }), {
      currentUserId: 'user-me',
      activeConversationId: 'conv-different',
    });

    expect(audioCtxMock).toHaveBeenCalledOnce();
    // Desktop notification suppressed because tab is focused.
    expect(notificationMock).not.toHaveBeenCalled();
  });

  it('truncates long message content to 100 chars in desktop notification', () => {
    const longContent = 'a'.repeat(150);

    handleNewMessageNotification(makeMessage({ content: longContent }), {
      currentUserId: 'user-me',
      activeConversationId: null,
    });

    expect(notificationMock).toHaveBeenCalledWith(
      'Alice',
      { body: `${'a'.repeat(100)}...`, icon: undefined },
    );
  });

  it('suppresses all notifications when muteAll is true', async () => {
    const { useNotifications } = await import('../stores/notification.store');
    vi.mocked(useNotifications).mockReturnValue({
      settings: {
        muteAll: true,
        mutedChannelIds: [],
        mutedServerIds: [],
        userId: '',
        allowDirectMessages: true,
        allowFriendRequests: true,
        showOnlineStatus: true,
        mentionKeywords: [],
      },
      channelOverrides: [],
      serverOverrides: [],
      isLoading: false,
      loadSettings: vi.fn(),
      updateSettings: vi.fn(),
      muteServer: vi.fn(),
      unmuteServer: vi.fn(),
      muteChannel: vi.fn(),
      unmuteChannel: vi.fn(),
      setChannelOverride: vi.fn(),
      loadChannelOverrides: vi.fn(),
      loadServerOverrides: vi.fn(),
      setServerNotificationLevel: vi.fn(),
      deleteServerNotificationOverride: vi.fn(),
      reset: vi.fn(),
    } as ReturnType<typeof useNotifications>);

    handleNewMessageNotification(makeMessage(), {
      currentUserId: 'user-me',
      activeConversationId: null,
    });

    expect(audioCtxMock).not.toHaveBeenCalled();
    expect(notificationMock).not.toHaveBeenCalled();
  });
});
