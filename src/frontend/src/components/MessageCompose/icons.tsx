// Icons for the message composer.
//
// Wrappers over the shared Icon primitive: one stroke weight for the whole app,
// and the composer keeps names that say what the control does.

import { Paperclip, BarChart3, Smile, Clock, Users, Send } from 'lucide-solid';
import { Icon } from '../ui/Icon';

export function PaperclipIcon(props: { class?: string }) {
  return <Icon icon={Paperclip} class={props.class ?? 'w-4 h-4'} />;
}

export function BarChartIcon(props: { class?: string }) {
  return <Icon icon={BarChart3} class={props.class ?? 'w-4 h-4'} />;
}

export function SmileIcon(props: { class?: string }) {
  return <Icon icon={Smile} class={props.class ?? 'w-4 h-4'} />;
}

export function ClockIcon(props: { class?: string }) {
  return <Icon icon={Clock} class={props.class ?? 'w-4 h-4'} />;
}

export function UsersIcon(props: { class?: string }) {
  return <Icon icon={Users} class={props.class ?? 'w-4 h-4'} />;
}

export function SendIcon(props: { class?: string }) {
  return <Icon icon={Send} class={props.class ?? 'w-4 h-4'} />;
}
