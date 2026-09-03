// Icons for the community bar and the room lists that reuse it.
//
// Wrappers over the shared Icon primitive so every glyph in the app is drawn at
// the same 1.5px weight. Glyph meanings are unchanged from the hand-inlined SVGs
// these replaced — only the weight and the source of truth moved.

import { Show } from 'solid-js';
import {
  Hash,
  Volume2,
  MessagesSquare,
  Radio,
  Settings,
  LogOut,
  Plus,
  ChevronDown,
  Shield,
} from 'lucide-solid';
import { Icon } from '../ui/Icon';
import { Capability, hasCapability } from '../../types/channel';

export function TextChannelIcon(props: { class?: string }) {
  return <Icon icon={Hash} class={props.class ?? 'w-4 h-4'} />;
}

export function VoiceChannelIcon(props: { class?: string }) {
  return <Icon icon={Volume2} class={props.class ?? 'w-4 h-4'} />;
}

export function ForumChannelIcon(props: { class?: string }) {
  return <Icon icon={MessagesSquare} class={props.class ?? 'w-4 h-4'} />;
}

export function StreamingChannelIcon(props: { class?: string }) {
  return <Icon icon={Radio} class={props.class ?? 'w-4 h-4'} />;
}

export function ChannelIcon(props: { capabilities: number; class?: string }) {
  return (
    <Show when={hasCapability(props.capabilities, Capability.Voice)} fallback={
      <Show when={hasCapability(props.capabilities, Capability.Streaming)} fallback={
        <Show when={hasCapability(props.capabilities, Capability.Forum)} fallback={<TextChannelIcon class={props.class} />}>
          <ForumChannelIcon class={props.class} />
        </Show>
      }>
        <StreamingChannelIcon class={props.class} />
      </Show>
    }>
      <VoiceChannelIcon class={props.class} />
    </Show>
  );
}

export function GearIcon(props: { class?: string }) {
  return <Icon icon={Settings} class={props.class ?? 'w-4 h-4'} />;
}

export function LogoutIcon(props: { class?: string }) {
  return <Icon icon={LogOut} class={props.class ?? 'w-4 h-4'} />;
}

export function PlusIcon(props: { class?: string }) {
  return <Icon icon={Plus} class={props.class} />;
}

export function ChevronDownIcon(props: { class?: string }) {
  return <Icon icon={ChevronDown} class={props.class} />;
}

export function AdminShieldIcon(props: { class?: string }) {
  return <Icon icon={Shield} class={props.class} />;
}
