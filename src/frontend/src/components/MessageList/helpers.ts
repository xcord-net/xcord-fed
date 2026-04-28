import type { Message } from '../../types/message';

// ManageMessages permission bit (must match backend Permission.ManageMessages = 1L << 7)
export const MANAGE_MESSAGES_BIT = 128n;

/** Returns true when this message should be visually grouped with the previous one
 *  (same author, within 5 minutes). */
export function shouldGroupWithPrevious(
  messages: readonly Message[],
  message: Message,
  index: number,
): boolean {
  if (index === 0) return false;
  const previousMessage = messages[index - 1];
  if (!previousMessage) return false;
  if (previousMessage.authorId !== message.authorId) return false;

  const timeDiff =
    new Date(message.createdAt).getTime() - new Date(previousMessage.createdAt).getTime();
  return timeDiff < 5 * 60 * 1000; // 5 minutes
}

/** Formats a timestamp as HH:MM in the user's locale. */
export function formatTime(dateString: string): string {
  const date = new Date(dateString);
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}
