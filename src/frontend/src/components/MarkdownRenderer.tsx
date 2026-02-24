import { For, createSignal } from 'solid-js';

interface MarkdownRendererProps {
  content: string;
}

type RenderedToken =
  | { type: 'text'; value: string }
  | { type: 'bold'; value: string }
  | { type: 'italic'; value: string }
  | { type: 'strikethrough'; value: string }
  | { type: 'code_inline'; value: string }
  | { type: 'code_block'; value: string }
  | { type: 'blockquote'; value: string }
  | { type: 'spoiler'; value: string }
  | { type: 'link'; value: string }
  | { type: 'mention_user'; value: string }
  | { type: 'mention_role'; value: string }
  | { type: 'mention_everyone' }
  | { type: 'mention_here' };

/**
 * Decode HTML entities produced by the backend's HtmlEncoder.Default.Encode().
 * The backend HTML-encodes message content for XSS prevention, so the frontend
 * must decode before parsing markdown syntax (especially newlines for code blocks
 * and angle brackets for mention patterns like <@userId>).
 */
function decodeHtmlEntities(text: string): string {
  return text
    .replace(/&#xA;/g, '\n')
    .replace(/&#xD;/g, '\r')
    .replace(/&#x9;/g, '\t')
    .replace(/&#x2B;/g, '+')
    .replace(/&#x27;/g, "'")
    .replace(/&quot;/g, '"')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&amp;/g, '&');
}

/**
 * Parse markdown content into a flat array of tokens.
 * Handles: bold, italic, strikethrough, code block, inline code, blockquote,
 * spoiler, links, and mention patterns (@everyone, @here, <@id>, <@&id>).
 *
 * The input content is HTML-encoded by the backend (HtmlEncoder.Default.Encode),
 * so we decode HTML entities first before parsing markdown syntax.
 */
export function parseMarkdown(content: string): RenderedToken[] {
  const decoded = decodeHtmlEntities(content);
  const tokens: RenderedToken[] = [];

  // Process the full content line by line first to handle blockquotes and code blocks
  const processInline = (text: string): RenderedToken[] => {
    const result: RenderedToken[] = [];
    let remaining = text;

    while (remaining.length > 0) {
      // @everyone
      const everyoneMatch = remaining.match(/^@everyone/);
      if (everyoneMatch) {
        result.push({ type: 'mention_everyone' });
        remaining = remaining.slice(everyoneMatch[0].length);
        continue;
      }

      // @here
      const hereMatch = remaining.match(/^@here/);
      if (hereMatch) {
        result.push({ type: 'mention_here' });
        remaining = remaining.slice(hereMatch[0].length);
        continue;
      }

      // <@&roleId> — role mention (must come before user mention)
      const roleMentionMatch = remaining.match(/^<@&(\d+)>/);
      if (roleMentionMatch) {
        result.push({ type: 'mention_role', value: roleMentionMatch[1] });
        remaining = remaining.slice(roleMentionMatch[0].length);
        continue;
      }

      // <@userId> — user mention
      const userMentionMatch = remaining.match(/^<@(\d+)>/);
      if (userMentionMatch) {
        result.push({ type: 'mention_user', value: userMentionMatch[1] });
        remaining = remaining.slice(userMentionMatch[0].length);
        continue;
      }

      // Bold: **text**
      const boldMatch = remaining.match(/^\*\*(.+?)\*\*/s);
      if (boldMatch) {
        result.push({ type: 'bold', value: boldMatch[1] });
        remaining = remaining.slice(boldMatch[0].length);
        continue;
      }

      // Italic: *text* or _text_ (single)
      const italicStarMatch = remaining.match(/^\*([^*]+?)\*/s);
      if (italicStarMatch) {
        result.push({ type: 'italic', value: italicStarMatch[1] });
        remaining = remaining.slice(italicStarMatch[0].length);
        continue;
      }
      const italicUnderscoreMatch = remaining.match(/^_([^_]+?)_/s);
      if (italicUnderscoreMatch) {
        result.push({ type: 'italic', value: italicUnderscoreMatch[1] });
        remaining = remaining.slice(italicUnderscoreMatch[0].length);
        continue;
      }

      // Strikethrough: ~~text~~
      const strikeMatch = remaining.match(/^~~(.+?)~~/s);
      if (strikeMatch) {
        result.push({ type: 'strikethrough', value: strikeMatch[1] });
        remaining = remaining.slice(strikeMatch[0].length);
        continue;
      }

      // Inline code: `code`
      const inlineCodeMatch = remaining.match(/^`([^`]+?)`/);
      if (inlineCodeMatch) {
        result.push({ type: 'code_inline', value: inlineCodeMatch[1] });
        remaining = remaining.slice(inlineCodeMatch[0].length);
        continue;
      }

      // Spoiler: ||text||
      const spoilerMatch = remaining.match(/^\|\|(.+?)\|\|/s);
      if (spoilerMatch) {
        result.push({ type: 'spoiler', value: spoilerMatch[1] });
        remaining = remaining.slice(spoilerMatch[0].length);
        continue;
      }

      // Link: https://... or http://...
      const linkMatch = remaining.match(/^https?:\/\/[^\s<>'"]+/);
      if (linkMatch) {
        result.push({ type: 'link', value: linkMatch[0] });
        remaining = remaining.slice(linkMatch[0].length);
        continue;
      }

      // Plain text — consume until the next potential token character
      const plainMatch = remaining.match(/^[^*_~`|@<h]+/) ||
        remaining.match(/^./);
      if (plainMatch) {
        const last = result[result.length - 1];
        if (last && last.type === 'text') {
          (last as { type: 'text'; value: string }).value += plainMatch[0];
        } else {
          result.push({ type: 'text', value: plainMatch[0] });
        }
        remaining = remaining.slice(plainMatch[0].length);
        continue;
      }

      break;
    }

    return result;
  };

  // Split into lines to handle blockquotes and code blocks
  const lines = decoded.split('\n');
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];

    // Code block: ```...```
    if (line.trimStart().startsWith('```')) {
      const codeLines: string[] = [];
      i++;
      while (i < lines.length && !lines[i].trimStart().startsWith('```')) {
        codeLines.push(lines[i]);
        i++;
      }
      i++; // skip closing ```
      tokens.push({ type: 'code_block', value: codeLines.join('\n') });
      continue;
    }

    // Blockquote: > text
    if (line.match(/^>\s/)) {
      const quoteContent = line.replace(/^>\s?/, '');
      tokens.push({ type: 'blockquote', value: quoteContent });
      i++;
      continue;
    }

    // Normal line — process inline
    const inlineTokens = processInline(line);
    tokens.push(...inlineTokens);

    // Add newline between lines (but not after the last line)
    if (i < lines.length - 1) {
      const last = tokens[tokens.length - 1];
      if (last && last.type === 'text') {
        (last as { type: 'text'; value: string }).value += '\n';
      } else {
        tokens.push({ type: 'text', value: '\n' });
      }
    }

    i++;
  }

  return tokens;
}

function SpoilerSpan(props: { value: string }) {
  const [revealed, setRevealed] = createSignal(false);
  return (
    <span
      class={`rounded px-1 cursor-pointer ${
        revealed()
          ? 'bg-xcord-bg-tertiary text-xcord-text-primary'
          : 'bg-xcord-bg-tertiary text-transparent hover:text-xcord-text-primary'
      }`}
      onClick={() => setRevealed(true)}
    >
      {props.value}
    </span>
  );
}

function renderToken(token: RenderedToken) {
  switch (token.type) {
    case 'bold':
      return <strong>{token.value}</strong>;
    case 'italic':
      return <em>{token.value}</em>;
    case 'strikethrough':
      return <s>{token.value}</s>;
    case 'code_inline':
      return (
        <code class="bg-xcord-bg-tertiary px-1 py-0.5 rounded text-sm font-mono">
          {token.value}
        </code>
      );
    case 'code_block':
      return (
        <pre>
          <code class="bg-xcord-bg-tertiary p-3 rounded text-sm font-mono block my-1 overflow-x-auto">
            {token.value}
          </code>
        </pre>
      );
    case 'blockquote':
      return (
        <blockquote class="border-l-4 border-xcord-text-muted pl-3 text-xcord-text-secondary">
          {token.value}
        </blockquote>
      );
    case 'spoiler':
      return <SpoilerSpan value={token.value} />;
    case 'link':
      return (
        <a
          href={token.value}
          target="_blank"
          rel="noopener noreferrer"
          class="text-blue-400 hover:underline"
        >
          {token.value}
        </a>
      );
    case 'mention_user':
      return (
        <span class="bg-blue-500/20 text-blue-400 rounded px-0.5">
          @{token.value}
        </span>
      );
    case 'mention_role':
      return (
        <span class="bg-blue-500/20 text-blue-400 rounded px-0.5">
          @{token.value}
        </span>
      );
    case 'mention_everyone':
      return (
        <span class="bg-yellow-500/20 text-yellow-400 rounded px-0.5">
          @everyone
        </span>
      );
    case 'mention_here':
      return (
        <span class="bg-yellow-500/20 text-yellow-400 rounded px-0.5">
          @here
        </span>
      );
    case 'text':
    default:
      return token.value;
  }
}

export default function MarkdownRenderer(props: MarkdownRendererProps) {
  // Guard against null/undefined content (e.g. system messages dispatched via
  // SignalR with an incomplete payload).  An empty string renders nothing safely.
  const tokens = () => parseMarkdown(props.content ?? '');

  return (
    <span class="break-words">
      <For each={tokens()}>
        {(token) => renderToken(token)}
      </For>
    </span>
  );
}
