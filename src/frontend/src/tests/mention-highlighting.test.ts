import { describe, it, expect } from 'vitest';
import { parseMarkdown } from '../components/MarkdownRenderer';

describe('mention highlighting', () => {
  describe('@everyone', () => {
    it('should parse @everyone as mention_everyone', () => {
      const tokens = parseMarkdown('@everyone');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('mention_everyone');
    });

    it('should parse @everyone within text', () => {
      const tokens = parseMarkdown('hey @everyone listen up');
      expect(tokens.some(t => t.type === 'mention_everyone')).toBe(true);
    });
  });

  describe('@here', () => {
    it('should parse @here as mention_here', () => {
      const tokens = parseMarkdown('@here');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('mention_here');
    });

    it('should parse @here within text', () => {
      const tokens = parseMarkdown('hey @here check this');
      expect(tokens.some(t => t.type === 'mention_here')).toBe(true);
    });
  });

  describe('<@userId>', () => {
    it('should parse user mention', () => {
      const tokens = parseMarkdown('<@123456789>');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('mention_user');
      expect((tokens[0] as { type: string; value: string }).value).toBe('123456789');
    });

    it('should parse user mention within text', () => {
      const tokens = parseMarkdown('hello <@987654321> how are you');
      const mention = tokens.find(t => t.type === 'mention_user');
      expect(mention).toBeDefined();
      expect((mention as { type: string; value: string }).value).toBe('987654321');
    });
  });

  describe('<@&roleId>', () => {
    it('should parse role mention', () => {
      const tokens = parseMarkdown('<@&111222333>');
      expect(tokens).toHaveLength(1);
      expect(tokens[0].type).toBe('mention_role');
      expect((tokens[0] as { type: string; value: string }).value).toBe('111222333');
    });

    it('should distinguish role mention from user mention', () => {
      const tokens = parseMarkdown('<@&111> vs <@222>');
      expect(tokens.some(t => t.type === 'mention_role')).toBe(true);
      expect(tokens.some(t => t.type === 'mention_user')).toBe(true);
    });
  });

  describe('combined mentions', () => {
    it('should parse multiple mentions in one message', () => {
      const tokens = parseMarkdown('@everyone and <@123> and <@&456>');
      expect(tokens.some(t => t.type === 'mention_everyone')).toBe(true);
      expect(tokens.some(t => t.type === 'mention_user')).toBe(true);
      expect(tokens.some(t => t.type === 'mention_role')).toBe(true);
    });
  });
});
