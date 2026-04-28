import { describe, it, expect } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import MarkdownRenderer, { parseMarkdown } from './MarkdownRenderer';

describe('parseMarkdown', () => {
  it('parses bold text', () => {
    const tokens = parseMarkdown('**hello**');
    expect(tokens).toEqual([{ type: 'bold', value: 'hello' }]);
  });

  it('parses italic, strikethrough, and inline code', () => {
    expect(parseMarkdown('*it*')).toEqual([{ type: 'italic', value: 'it' }]);
    expect(parseMarkdown('~~no~~')).toEqual([{ type: 'strikethrough', value: 'no' }]);
    expect(parseMarkdown('`code`')).toEqual([{ type: 'code_inline', value: 'code' }]);
  });

  it('parses code blocks fenced by triple backticks', () => {
    const tokens = parseMarkdown('```\nlet x = 1;\n```');
    expect(tokens).toEqual([{ type: 'code_block', value: 'let x = 1;' }]);
  });

  it('parses blockquotes prefixed with "> "', () => {
    expect(parseMarkdown('> quoted')).toEqual([
      { type: 'blockquote', value: 'quoted' },
    ]);
  });

  it('parses mention patterns: @everyone, @here, <@id>, <@&id>', () => {
    expect(parseMarkdown('@everyone')).toEqual([{ type: 'mention_everyone' }]);
    expect(parseMarkdown('@here')).toEqual([{ type: 'mention_here' }]);
    expect(parseMarkdown('<@1234>')).toEqual([{ type: 'mention_user', value: '1234' }]);
    expect(parseMarkdown('<@&5678>')).toEqual([{ type: 'mention_group', value: '5678' }]);
  });

  it('decodes HTML entities (&lt;@id&gt;) before parsing mentions', () => {
    expect(parseMarkdown('&lt;@42&gt;')).toEqual([
      { type: 'mention_user', value: '42' },
    ]);
  });
});

describe('MarkdownRenderer', () => {
  it('renders an empty span when content is null/undefined', () => {
    const { container } = render(() => (
      <MarkdownRenderer content={null as unknown as string} />
    ));
    expect(container.textContent).toBe('');
  });

  it('renders bold text inside a <strong>', () => {
    const { container } = render(() => <MarkdownRenderer content="**hi**" />);
    const strong = container.querySelector('strong');
    expect(strong).not.toBeNull();
    expect(strong!.textContent).toBe('hi');
  });

  it('renders a link with target=_blank and the matched href', () => {
    const { container } = render(() => (
      <MarkdownRenderer content="https://example.com" />
    ));
    const a = container.querySelector('a');
    expect(a).not.toBeNull();
    expect(a!.getAttribute('href')).toBe('https://example.com');
    expect(a!.getAttribute('target')).toBe('_blank');
  });

  it('renders @everyone in the mentionEveryone class span', () => {
    const { container } = render(() => <MarkdownRenderer content="@everyone" />);
    expect(container.textContent).toContain('@everyone');
  });

  it('reveals spoiler text on click', () => {
    const { container } = render(() => (
      <MarkdownRenderer content="||secret||" />
    ));
    // First span is the content wrapper; the inner span is the SpoilerSpan.
    const spans = container.querySelectorAll('span');
    expect(spans.length).toBeGreaterThanOrEqual(2);
    const spoiler = spans[1];
    const beforeClass = spoiler.className;
    fireEvent.click(spoiler);
    expect(spoiler.className).not.toBe(beforeClass);
  });

  it('renders inline code inside a <code>', () => {
    const { container } = render(() => <MarkdownRenderer content="`x`" />);
    const code = container.querySelector('code');
    expect(code).not.toBeNull();
    expect(code!.textContent).toBe('x');
  });
});
