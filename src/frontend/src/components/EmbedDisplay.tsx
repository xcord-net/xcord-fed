import { Show } from 'solid-js';
import type { MessageEmbed } from '../types/message';
import styles from './EmbedDisplay.module.css';

interface EmbedDisplayProps {
  embed: MessageEmbed;
}

const HEX_COLOR_RE = /^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/;

export default function EmbedDisplay(props: EmbedDisplayProps) {
  const borderColor = () => {
    const c = props.embed.color;
    return c && HEX_COLOR_RE.test(c) ? c : '#d4943a';
  };

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
