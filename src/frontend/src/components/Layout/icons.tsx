// Icons for the Layout header bar.
//
// These were hand-inlined SVGs at a 2px stroke. They now delegate to the shared
// Icon primitive so the whole app draws at one weight; the named wrappers stay
// because call sites read better as <SearchIcon /> than as a lucide import plus
// a size decision at every use.

import { Search, Pin, MessagesSquare, Settings } from 'lucide-solid';
import { Icon } from '../ui/Icon';

export function SearchIcon(props: { class?: string }) {
  return <Icon icon={Search} class={props.class ?? 'w-4 h-4'} />;
}

export function PinIcon(props: { class?: string }) {
  return <Icon icon={Pin} class={props.class ?? 'w-4 h-4'} />;
}

export function ThreadsIcon(props: { class?: string }) {
  return <Icon icon={MessagesSquare} class={props.class ?? 'w-4 h-4'} />;
}

export function GearIcon(props: { class?: string }) {
  return <Icon icon={Settings} class={props.class ?? 'w-4 h-4'} />;
}
