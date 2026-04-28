import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import EmbedDisplay from './EmbedDisplay';
import type { MessageEmbed } from '../types/message';

describe('EmbedDisplay', () => {
  it('renders title as a link to the embed URL', () => {
    const embed: MessageEmbed = { title: 'Hello', url: 'https://example.com' };
    const { getByRole } = render(() => <EmbedDisplay embed={embed} />);
    const link = getByRole('link');
    expect(link).toHaveTextContent('Hello');
    expect(link.getAttribute('href')).toBe('https://example.com');
    expect(link.getAttribute('rel')).toContain('noopener');
  });

  it('renders description and site name when present', () => {
    const embed: MessageEmbed = { siteName: 'Site', description: 'A description' };
    const { container } = render(() => <EmbedDisplay embed={embed} />);
    expect(container.textContent).toContain('Site');
    expect(container.textContent).toContain('A description');
  });

  it('renders an image when imageUrl is present', () => {
    const embed: MessageEmbed = { title: 'Pic', imageUrl: 'https://example.com/img.png' };
    const { container } = render(() => <EmbedDisplay embed={embed} />);
    const img = container.querySelector('img')!;
    expect(img.getAttribute('src')).toBe('https://example.com/img.png');
    expect(img.getAttribute('alt')).toBe('Pic');
  });

  it('uses the embed color for the border when valid hex', () => {
    const { container } = render(() => <EmbedDisplay embed={{ color: '#abcdef' }} />);
    const root = container.firstChild as HTMLElement;
    // jsdom serializes #abcdef → rgb(171, 205, 239)
    expect(root.style.borderLeftColor).toBe('rgb(171, 205, 239)');
  });

  it('falls back to default border color for invalid hex', () => {
    const { container } = render(() => <EmbedDisplay embed={{ color: 'not-a-color' }} />);
    const root = container.firstChild as HTMLElement;
    // #d4943a → rgb(212, 148, 58)
    expect(root.style.borderLeftColor).toBe('rgb(212, 148, 58)');
  });

  it('omits optional sections when fields are missing', () => {
    const { container } = render(() => <EmbedDisplay embed={{}} />);
    expect(container.querySelector('a')).toBeNull();
    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('p')).toBeNull();
  });
});
