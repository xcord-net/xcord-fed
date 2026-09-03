// Glyphs used across feature folders.
//
// Folder-local icons live next to the components that use them (Layout/icons.tsx,
// MessageCompose/icons.tsx, Sidebar/icons.tsx). These are the ones more than one
// folder needs — the screen-share monitor alone was inlined five times in three
// files before this existed.

import { Monitor, MicOff, Menu, X, Shield, TriangleAlert } from 'lucide-solid';
import { Icon } from './Icon';

export function ScreenShareIcon(props: { class?: string }) {
  return <Icon icon={Monitor} class={props.class} />;
}

export function MutedIcon(props: { class?: string; label?: string }) {
  return <Icon icon={MicOff} class={props.class} label={props.label} />;
}

export function MenuIcon(props: { class?: string; size?: number }) {
  return <Icon icon={Menu} class={props.class} size={props.size} />;
}

export function CloseIcon(props: { class?: string }) {
  return <Icon icon={X} class={props.class} />;
}

export function ShieldIcon(props: { class?: string }) {
  return <Icon icon={Shield} class={props.class} />;
}

export function AlertIcon(props: { class?: string }) {
  return <Icon icon={TriangleAlert} class={props.class} />;
}
