import type { Message } from '../../types/message';

/**
 * System message types the backend posts into a server's system channel. These
 * carry no author and no content - the line the user reads is derived from the
 * type plus the JSON in `metadata`.
 */
const SYSTEM_MESSAGE_TYPES = new Set([
  'MemberJoin',
  'MemberLeave',
  'MemberKick',
  'MemberBan',
  'ChannelNameChange',
  'ChannelTopicChange',
  'PinnedMessage',
  'RoleCreated',
  'RoleDeleted',
  'ServerUpdated',
  'InviteCreated',
  'ThreadCreated',
  'ThreadArchived',
  'ScheduledEventCreated',
  'CallStarted',
]);

export function isSystemMessage(message: Message): boolean {
  return SYSTEM_MESSAGE_TYPES.has(message.type);
}

interface SystemMetadata {
  username?: string;
  displayName?: string;
  reason?: string;
  name?: string;
}

/**
 * Metadata arrives as a JSON string from the API and, depending on the handler,
 * with either PascalCase or camelCase keys. Read it case-insensitively rather than
 * betting on one shape.
 */
function readMetadata(message: Message): SystemMetadata {
  const raw = message.metadata;
  if (!raw) return {};

  let parsed: Record<string, unknown>;
  try {
    parsed = typeof raw === 'string' ? JSON.parse(raw) : (raw as Record<string, unknown>);
  } catch {
    return {};
  }
  if (!parsed || typeof parsed !== 'object') return {};

  const lowered: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(parsed)) {
    lowered[key.toLowerCase()] = value;
  }

  const str = (key: string) =>
    typeof lowered[key] === 'string' && lowered[key] ? (lowered[key] as string) : undefined;

  return {
    username: str('username'),
    displayName: str('displayname'),
    reason: str('reason'),
    name: str('name'),
  };
}

/**
 * The line shown for a system message. Every known type gets its own wording and
 * anything unrecognised falls back to a readable label, so a system message can
 * never render as a blank row.
 */
export function systemMessageText(message: Message): string {
  const meta = readMetadata(message);
  const who = meta.displayName || meta.username || 'Someone';
  const because = meta.reason ? ` (${meta.reason})` : '';

  switch (message.type) {
    case 'MemberJoin':
      return `${who} joined the server`;
    case 'MemberLeave':
      return `${who} left the server`;
    case 'MemberKick':
      return `${who} was kicked from the server${because}`;
    case 'MemberBan':
      return `${who} was banned from the server${because}`;
    case 'ChannelNameChange':
      return meta.name ? `The channel was renamed to ${meta.name}` : 'The channel was renamed';
    case 'ChannelTopicChange':
      return 'The channel topic was changed';
    case 'PinnedMessage':
      return 'A message was pinned to this channel';
    case 'RoleCreated':
      return meta.name ? `The ${meta.name} group was created` : 'A group was created';
    case 'RoleDeleted':
      return meta.name ? `The ${meta.name} group was deleted` : 'A group was deleted';
    case 'ServerUpdated':
      return 'The server settings were updated';
    case 'InviteCreated':
      return `${who} created an invite`;
    case 'ThreadCreated':
      return meta.name ? `A thread was started: ${meta.name}` : 'A thread was started';
    case 'ThreadArchived':
      return 'A thread was archived';
    case 'ScheduledEventCreated':
      return meta.name ? `An event was scheduled: ${meta.name}` : 'An event was scheduled';
    case 'CallStarted':
      return `${who} started a call`;
    default:
      // Unreachable for known types, but a new backend type must still read as
      // something rather than as an empty row.
      return 'Channel activity';
  }
}
