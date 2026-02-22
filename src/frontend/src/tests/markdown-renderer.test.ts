import { describe, it, expect } from 'vitest';
import { parseMarkdown } from '../components/MarkdownRenderer';

describe('parseMarkdown', () => {
  describe('bold', () => {
    it('should parse **bold** text', () => {
      const tokens = parseMarkdown('**hello**');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('bold');
      expect((tokens[0] as { type: string; value: string }).value).toBe('hello');
    });

    it('should parse bold within surrounding text', () => {
      const tokens = parseMarkdown('before **bold** after');
      expect(tokens.some(t => t.type === 'bold')).toBe(true);
      const bold = tokens.find(t => t.type === 'bold');
      expect((bold as { type: string; value: string }).value).toBe('bold');
    });
  });

  describe('italic', () => {
    it('should parse *italic* text', () => {
      const tokens = parseMarkdown('*hello*');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('italic');
      expect((tokens[0] as { type: string; value: string }).value).toBe('hello');
    });

    it('should parse _italic_ text', () => {
      const tokens = parseMarkdown('_hello_');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('italic');
      expect((tokens[0] as { type: string; value: string }).value).toBe('hello');
    });
  });

  describe('strikethrough', () => {
    it('should parse ~~strikethrough~~ text', () => {
      const tokens = parseMarkdown('~~deleted~~');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('strikethrough');
      expect((tokens[0] as { type: string; value: string }).value).toBe('deleted');
    });
  });

  describe('inline code', () => {
    it('should parse `inline code`', () => {
      const tokens = parseMarkdown('`const x = 1`');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('code_inline');
      expect((tokens[0] as { type: string; value: string }).value).toBe('const x = 1');
    });
  });

  describe('code block', () => {
    it('should parse code block spanning multiple lines', () => {
      const tokens = parseMarkdown('```\nconst x = 1\nconsole.log(x)\n```');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('code_block');
      expect((tokens[0] as { type: string; value: string }).value).toContain('const x = 1');
    });
  });

  describe('blockquote', () => {
    it('should parse > blockquote', () => {
      const tokens = parseMarkdown('> This is a quote');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('blockquote');
      expect((tokens[0] as { type: string; value: string }).value).toBe('This is a quote');
    });
  });

  describe('spoiler', () => {
    it('should parse ||spoiler|| text', () => {
      const tokens = parseMarkdown('||hidden text||');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('spoiler');
      expect((tokens[0] as { type: string; value: string }).value).toBe('hidden text');
    });
  });

  describe('links', () => {
    it('should parse https:// URLs', () => {
      const tokens = parseMarkdown('https://example.com');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('link');
      expect((tokens[0] as { type: string; value: string }).value).toBe('https://example.com');
    });

    it('should parse http:// URLs', () => {
      const tokens = parseMarkdown('http://example.com/path');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('link');
    });
  });

  describe('plain text', () => {
    it('should return plain text as-is', () => {
      const tokens = parseMarkdown('Hello world');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('text');
      expect((tokens[0] as { type: string; value: string }).value).toBe('Hello world');
    });

    it('should handle empty string', () => {
      const tokens = parseMarkdown('');
      expect(tokens).toHaveLength(0);
    });
  });

  describe('mixed content', () => {
    it('should parse bold and italic in same string', () => {
      const tokens = parseMarkdown('**bold** and *italic*');
      expect(tokens.some(t => t.type === 'bold')).toBe(true);
      expect(tokens.some(t => t.type === 'italic')).toBe(true);
    });

    it('should keep plain text between formatted tokens', () => {
      const tokens = parseMarkdown('hello **world** foo');
      const textTokens = tokens.filter(t => t.type === 'text');
      expect(textTokens.length).toBeGreaterThan(0);
    });
  });
});
