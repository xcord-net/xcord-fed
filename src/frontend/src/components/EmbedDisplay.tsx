import { Show } from 'solid-js';
import type { MessageEmbed } from '../types/message';

interface EmbedDisplayProps {
  embed: MessageEmbed;
}

export default function EmbedDisplay(props: EmbedDisplayProps) {
  const borderColor = () => props.embed.color ?? '#5865f2'; // xcord-brand fallback

  return (
    <div
      class="mt-1 max-w-lg rounded-r bg-xcord-bg-tertiary border-l-4 pl-3 pr-3 py-2"
      style={{ 'border-left-color': borderColor() }}
    >
      <Show when={props.embed.siteName}>
        <div class="text-xcord-text-muted text-xs mb-0.5">{props.embed.siteName}</div>
      </Show>

      <Show when={props.embed.title}>
        <a
          href={props.embed.url}
          target="_blank"
          rel="noopener noreferrer"
          class="text-blue-400 font-semibold text-sm hover:underline block"
        >
          {props.embed.title}
        </a>
      </Show>

      <Show when={props.embed.description}>
        <p class="text-xcord-text-secondary text-sm line-clamp-3 mt-0.5">
          {props.embed.description}
        </p>
      </Show>

      <Show when={props.embed.imageUrl}>
        <img
          src={props.embed.imageUrl}
          alt={props.embed.title ?? 'Embed image'}
          class="rounded max-w-md max-h-64 object-cover mt-2"
        />
      </Show>
    </div>
  );
}
