import { Show, createMemo } from 'solid-js';
import { useTyping } from '../stores/typing.store';

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
      <div id="typing-indicator" class="px-4 py-1 flex items-center space-x-2 bg-xcord-bg-secondary/50">
        {/* Animated dots */}
        <span class="flex items-center space-x-0.5">
          <span class="w-1 h-1 bg-xcord-text-muted rounded-full animate-bounce [animation-delay:-0.3s]" />
          <span class="w-1 h-1 bg-xcord-text-muted rounded-full animate-bounce [animation-delay:-0.15s]" />
          <span class="w-1 h-1 bg-xcord-text-muted rounded-full animate-bounce" />
        </span>
        <span class="text-xcord-text-muted text-sm">{typingText()}</span>
      </div>
    </Show>
  );
}
