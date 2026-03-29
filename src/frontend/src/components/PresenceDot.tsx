import { createMemo } from 'solid-js';
import { usePresence } from '../stores/presence.store';
import type { PresenceStatus } from '../types/presence';
import styles from './PresenceDot.module.css';

interface PresenceDotProps {
  userId: string;
  size?: 'sm' | 'md' | 'lg';
}

export const statusClassMap: Record<PresenceStatus, string> = {
  online: styles.statusOnline,
  idle: styles.statusIdle,
  dnd: styles.statusDnd,
  offline: styles.statusOffline,
};

export function getStatusClass(status: PresenceStatus): string {
  return statusClassMap[status];
}

const sizeClassMap: Record<'sm' | 'md' | 'lg', string> = {
  sm: styles.sizeSm,
  md: styles.sizeMd,
  lg: styles.sizeLg,
};

export default function PresenceDot(props: PresenceDotProps) {
  const presence = usePresence();

  const status = createMemo(() => presence.getPresence(props.userId));
  const colorClass = createMemo(() => statusClassMap[status()]);
  const sizeClass = createMemo(() => sizeClassMap[props.size ?? 'sm']);

  return (
    <div
      class={`${styles.dot} ${colorClass()} ${sizeClass()}`}
      aria-label={`Status: ${status()}`}
    />
  );
}
