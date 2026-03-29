import { Show, createMemo } from 'solid-js';
import { useTyping } from '../stores/typing.store';
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

export default function TypingIndicator(props: TypingIndicatorProps) {
  const typing = useTyping();

  const typingUsers = createMemo(() => typing.getTypingUsers(props.conversationId));
  const typingText = createMemo(() => formatTypingText(typingUsers()));

  return (
    <Show when={typingUsers().length > 0}>
      <div id="typing-indicator" class={styles.container}>
        {/* Animated dots */}
        <span class={styles.dots}>
          <span class={styles.dot} />
          <span class={styles.dot} />
          <span class={styles.dot} />
        </span>
        <span class={styles.text}>{typingText()}</span>
      </div>
    </Show>
  );
}
