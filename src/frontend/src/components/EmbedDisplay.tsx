import { Show } from 'solid-js';
import type { MessageEmbed } from '../types/message';
import styles from './EmbedDisplay.module.css';

interface EmbedDisplayProps {
  embed: MessageEmbed;
}

export default function EmbedDisplay(props: EmbedDisplayProps) {
  const borderColor = () => props.embed.color ?? '#d4943a'; // xcord-brand fallback

  return (
    <div
      class={styles.embed}
      style={{ 'border-left-color': borderColor() }}
    >
      <Show when={props.embed.siteName}>
        <div class={styles.siteName}>{props.embed.siteName}</div>
      </Show>

      <Show when={props.embed.title}>
        <a
          href={props.embed.url}
          target="_blank"
          rel="noopener noreferrer"
          class={styles.titleLink}
        >
          {props.embed.title}
        </a>
      </Show>

      <Show when={props.embed.description}>
        <p class={styles.description}>
          {props.embed.description}
        </p>
      </Show>

      <Show when={props.embed.imageUrl}>
        <img
          src={props.embed.imageUrl}
          alt={props.embed.title ?? 'Embed image'}
          class={styles.image}
        />
      </Show>
    </div>
  );
}
