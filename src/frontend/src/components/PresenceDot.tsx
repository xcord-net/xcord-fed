import { createMemo } from 'solid-js';
import { usePresence } from '../stores/presence.store';
import type { PresenceStatus } from '../types/presence';

interface PresenceDotProps {
  userId: string;
  size?: 'sm' | 'md' | 'lg';
}

export const statusColorMap: Record<PresenceStatus, string> = {
  online: 'bg-green-500',
  idle: 'bg-yellow-500',
  dnd: 'bg-red-500',
  offline: 'bg-gray-500',
};

export function getStatusColor(status: PresenceStatus): string {
  return statusColorMap[status];
}

const sizeMap: Record<'sm' | 'md' | 'lg', string> = {
  sm: 'w-2 h-2',
  md: 'w-2.5 h-2.5',
  lg: 'w-3 h-3',
};

export default function PresenceDot(props: PresenceDotProps) {
  const presence = usePresence();

  const status = createMemo(() => presence.getPresence(props.userId));
  const colorClass = createMemo(() => statusColorMap[status()]);
  const sizeClass = createMemo(() => sizeMap[props.size ?? 'sm']);

  return (
    <div
      class={`absolute bottom-0 right-0 rounded-full border-2 border-xcord-bg-secondary ${colorClass()} ${sizeClass()}`}
      aria-label={`Status: ${status()}`}
    />
  );
}
