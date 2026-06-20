import { Show, createMemo } from 'solid-js';
import { useTyping } from '../stores/typing.store';
import { useMembers } from '../stores/member.store';
import type { Member } from '../types/member';
import Flexbox from './ui/Flexbox';
import styles from './TypingIndicator.module.css';

interface TypingIndicatorProps {
  conversationId: string;
}

export function formatTypingText(users: string[]): string {
  if (users.length === 0) return '';
  if (users.length === 1) return `${users[0]} is typing...`;
  if (users.length === 2) return `${users[0]} and ${users[1]} are typing...`;
  const others = users.length - 2;
  return `${users[0]}, ${users[1]}, and ${others} other${others === 1 ? '' : 's'} are typing...`;
}

/**
 * Maps typing user ids to human-readable names. Unknown users (e.g. members not
 * loaded for this server, or DM participants) resolve to "Someone" - the raw
 * Snowflake id is never surfaced to the user.
 */
export function resolveTypingNames(userIds: string[], members: Member[]): string[] {
  return userIds.map((id) => {
    const m = members.find((member) => member.userId === id);
    return m ? (m.nickname || m.displayName || m.username) : 'Someone';
  });
}

export default function TypingIndicator(props: TypingIndicatorProps) {
  const typing = useTyping();
  const memberStore = useMembers();

  const typingUsers = createMemo(() => typing.getTypingUsers(props.conversationId));
  const typingNames = createMemo(() => resolveTypingNames(typingUsers(), memberStore.members));
  const typingText = createMemo(() => formatTypingText(typingNames()));

  return (
    <Show when={typingUsers().length > 0}>
      <Flexbox align="center" gap={0.5} id="typing-indicator" class={styles.container}>
        {/* Animated dots */}
        <span class={styles.dots}>
          <span class={styles.dot} />
          <span class={styles.dot} />
          <span class={styles.dot} />
        </span>
        <span class={styles.text}>{typingText()}</span>
      </Flexbox>
    </Show>
  );
}
