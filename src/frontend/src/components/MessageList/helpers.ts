import type { Message } from '../../types/message';

// ManageMessages permission bit (must match backend Permission.ManageMessages = 1L << 7)
export const MANAGE_MESSAGES_BIT = 128n;

/** Returns true when this message should be visually grouped with the previous one
 *  (same author, within 5 minutes). A reply is never grouped: grouping drops the
 *  header, which would hide the quote line saying who is being answered. */
export function shouldGroupWithPrevious(
  messages: readonly Message[],
  message: Message,
  index: number,
): boolean {
  if (index === 0) return false;
  if (message.replyToId || message.replyTo) return false;
  const previousMessage = messages[index - 1];
  if (!previousMessage) return false;
  if (previousMessage.authorId !== message.authorId) return false;

  const timeDiff =
    new Date(message.createdAt).getTime() - new Date(previousMessage.createdAt).getTime();
  return timeDiff < 5 * 60 * 1000; // 5 minutes
}

/**
 * True when this message is the first of a new calendar day, so the list can
 * put a divider above it. Index 0 always starts a day: the top of a loaded page
 * needs the date, otherwise the oldest messages on screen have no date at all.
 *
 * Comparison is on local calendar date, not elapsed time - two messages 40
 * minutes apart still belong to different days if one is before midnight.
 */
export function startsNewDay(
  messages: readonly Message[],
  message: Message,
  index: number,
): boolean {
  if (index === 0) return true;
  const previous = messages[index - 1];
  if (!previous) return true;
  return !isSameLocalDay(new Date(previous.createdAt), new Date(message.createdAt));
}

function isSameLocalDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}

/**
 * Divider label: "Today" and "Yesterday" read faster than a date for the two
 * days people actually scroll through; anything older gets the real date.
 */
export function formatDayLabel(iso: string, now: Date = new Date()): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  if (isSameLocalDay(date, now)) return 'Today';

  const yesterday = new Date(now);
  yesterday.setDate(yesterday.getDate() - 1);
  if (isSameLocalDay(date, yesterday)) return 'Yesterday';

  const sameYear = date.getFullYear() === now.getFullYear();
  return date.toLocaleDateString(undefined, {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
    ...(sameYear ? {} : { year: 'numeric' }),
  });
}

// Re-exported from the shared datetime util so message timestamps use the same
// formatting as the rest of the app.
export { formatTime } from '../../utils/datetime';
