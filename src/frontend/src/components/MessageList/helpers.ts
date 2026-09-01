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

// Re-exported from the shared datetime util so message timestamps use the same
// formatting as the rest of the app.
export { formatTime } from '../../utils/datetime';
