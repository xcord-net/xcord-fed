import { For, Show } from 'solid-js';
import type { MessageAttachment } from '../../types/message';
import Flexbox from '../ui/Flexbox';
import styles from './AttachmentList.module.css';

interface AttachmentListProps {
  attachments: MessageAttachment[];
}

/** Renders the attachment list for a message, showing image thumbnails and file links. */
export default function AttachmentList(props: AttachmentListProps) {
  return (
    <Flexbox wrap="wrap" gap={0.5} class={styles.attachmentList}>
      <For each={props.attachments}>
        {(attachment) => (
          // Which of the two shapes an attachment takes depends on whether its
          // thumbnail has been generated yet, so "there is an attachment here"
          // needs a hook that does not depend on winning that race.
          <div data-testid="message-attachment" class={styles.attachmentItem}>
          <Show
            when={attachment.thumbnailUrl}
            fallback={
              <a
                data-testid="message-attachment-link"
                href={attachment.downloadUrl}
                target="_blank"
                rel="noopener noreferrer"
                class={styles.attachmentLink}
                aria-label={`Download ${attachment.fileName}`}
              >
                {attachment.fileName}
              </a>
            }
          >
            <a
              data-testid="message-attachment-image"
              href={attachment.downloadUrl}
              target="_blank"
              rel="noopener noreferrer"
              aria-label={`View ${attachment.fileName}`}
            >
              <img
                src={attachment.thumbnailUrl}
                alt={attachment.fileName}
                class={styles.attachmentThumbnail}
                data-attachment-thumbnail
              />
            </a>
          </Show>
          </div>
        )}
      </For>
    </Flexbox>
  );
}
