// notification.service.ts
// Handles browser Notification API permission, desktop notifications, and sound playback.
// Tab focus is tracked via document.hasFocus() and focus/blur event listeners.

import { useNotifications } from '../stores/notification.store';

// Short sine-wave beep encoded as a base64 WAV (44-byte header + 882 samples @22050Hz).
// Generated offline so no network fetch is required at runtime.
const BEEP_WAV_B64 =
  'UklGRlQDAABXQVZFZm10IBAAAAABAAEARKwAAIhYAQACABAAZGF0YTADAAA' +
  'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA' +
  'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA' +
  'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAB+AD4AngDsAEoBpAHsASwCXQJ+ApAC' +
  'lAKMAn4CZAIyAugBhgEQAY8AAwBs/8v+I/53/c/8L/yX+w77kPok+sz5h/' +
  'lV+TT5I/kl+Tn5Xvma+ez5WPrh+oP7Pvwb/RL+H/9AAHMB9ALwBO4G8Qj3' +
  'Cv0MAQ8EEQkTDhUQFxMZFhsYHRofHCAfIiEkIyUlJiYmJiYmJiYlJSQjIiAe' +
  'HBkWExAMCAQA/Pjz7ujj3NXOx8C5sq2opqOnqrC4wcnT3efw+AEJEC0YSSBp' +
  'Jooujza/PARCMEhNTlZSR1UjVuFVhFQKU3VRwk/sTfVL4Um1R29FEUOmQCk+' +
  'mTv6OFE2njPtMD4ukCvtKFcm0CNUIO0cmBlaFi0TFRMVFBUUGBUVFBQSEA0K' +
  'BgIB/Pj08e7s6+vr7O3v8vX4/P8DCAwQFBcaHR8gICAfHRoXEw4IAgD69vLs' +
  '6OTg3NjU0c3KyMfGx8nLz9PX2+Dl6Ozy9vsBBgsQFBgbHh8fHh0bGBQPCgQA' +
  '+vXw6+bi3trX1NPT1NXX2dze4OLj5OXl5OPh3tvX0s3IxsTE';

// Track focus state module-level so it survives function calls.
let _tabFocused = typeof document !== 'undefined' ? document.hasFocus() : true;

if (typeof window !== 'undefined') {
  window.addEventListener('focus', () => { _tabFocused = true; });
  window.addEventListener('blur', () => { _tabFocused = false; });
}

/** Returns true when the browser tab currently has user focus. */
export function isTabFocused(): boolean {
  return _tabFocused;
}

/**
 * Request browser Notification API permission.
 * Safe to call multiple times — resolves immediately if already granted/denied.
 */
export async function requestPermission(): Promise<NotificationPermission> {
  if (typeof Notification === 'undefined') {
    return 'denied';
  }
  if (Notification.permission !== 'default') {
    return Notification.permission;
  }
  return Notification.requestPermission();
}

/**
 * Play a short notification beep using the Web Audio API.
 * Falls back gracefully if the browser does not support AudioContext.
 */
export function playSound(): void {
  try {
    // Build a short sine-wave tone via AudioContext — no network fetch needed.
    const AudioContextCtor =
      (window as Window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext ??
      window.AudioContext;

    if (!AudioContextCtor) {
      // Last-resort fallback: data-URI WAV via HTMLAudioElement
      const audio = new Audio(`data:audio/wav;base64,${BEEP_WAV_B64}`);
      audio.volume = 0.3;
      audio.play().catch(() => { /* autoplay may be blocked */ });
      return;
    }

    const ctx = new AudioContextCtor();
    const oscillator = ctx.createOscillator();
    const gain = ctx.createGain();

    oscillator.connect(gain);
    gain.connect(ctx.destination);

    oscillator.type = 'sine';
    oscillator.frequency.setValueAtTime(880, ctx.currentTime);            // A5
    gain.gain.setValueAtTime(0.25, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.3); // fade out

    oscillator.start(ctx.currentTime);
    oscillator.stop(ctx.currentTime + 0.3);

    // AudioContext must be closed after the sound finishes to free resources.
    oscillator.onended = () => { ctx.close(); };
  } catch {
    // Silently ignore — notification sound is non-critical.
  }
}

/**
 * Show a desktop (OS-level) notification using the Notification API.
 * Does nothing if permission has not been granted.
 */
export function showDesktopNotification(
  title: string,
  body: string,
  icon?: string,
): Notification | null {
  if (typeof Notification === 'undefined' || Notification.permission !== 'granted') {
    return null;
  }
  try {
    const notif = new Notification(title, { body, icon });
    return notif;
  } catch {
    return null;
  }
}

export interface NotificationContext {
  /** ID of the current user — suppress own-message notifications. */
  currentUserId: string | null;
  /** ConversationId currently displayed in the main pane. */
  activeConversationId: string | null;
}

/**
 * Decide whether and how to notify the user about a new message, then fire
 * the appropriate side-effects (sound, desktop notification).
 *
 * Suppression rules (in priority order):
 *   1. Message is from the current user.
 *   2. Notification settings have muteAll = true.
 *   3. The message's conversation is in mutedChannelIds.
 *   4. Tab is focused AND the message is in the active conversation.
 */
export function handleNewMessageNotification(
  message: { authorId: string; conversationId: string; content: string; authorUsername?: string },
  ctx: NotificationContext,
): void {
  // 1. Never notify for own messages.
  if (ctx.currentUserId && message.authorId === ctx.currentUserId) {
    return;
  }

  const notifStore = useNotifications();
  const settings = notifStore.settings;

  // 2. Respect muteAll toggle.
  if (settings?.muteAll) {
    return;
  }

  // 3. Respect muted channels.
  if (settings?.mutedChannelIds.includes(message.conversationId)) {
    return;
  }

  // 4. Tab focused and user is actively viewing this conversation — no noise.
  if (isTabFocused() && ctx.activeConversationId === message.conversationId) {
    return;
  }

  // Play audio feedback.
  playSound();

  // Show desktop notification only when the tab is not focused.
  if (!isTabFocused()) {
    const sender = message.authorUsername ?? 'Someone';
    const preview =
      message.content.length > 100
        ? `${message.content.slice(0, 100)}...`
        : message.content;
    showDesktopNotification(sender, preview);
  }
}
