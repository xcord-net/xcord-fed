import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import { ScreenShareIcon, MutedIcon, MenuIcon, CloseIcon, ShieldIcon, AlertIcon } from './icons';

const wrappers = [
  ['ScreenShareIcon', ScreenShareIcon],
  ['MutedIcon', MutedIcon],
  ['MenuIcon', MenuIcon],
  ['CloseIcon', CloseIcon],
  ['ShieldIcon', ShieldIcon],
  ['AlertIcon', AlertIcon],
] as const;

describe('shared icons', () => {
  it.each(wrappers)('%s draws at the one stroke weight', (_name, Glyph) => {
    const { container } = render(() => <Glyph />);
    expect(container.querySelector('svg')?.getAttribute('stroke-width')).toBe('1.5');
  });

  it.each(wrappers)('%s takes the class its call site gives it', (_name, Glyph) => {
    const { container } = render(() => <Glyph class="sized" />);
    expect(container.querySelector('svg')?.getAttribute('class')).toContain('sized');
  });

  it.each(wrappers)('%s is decorative unless named', (_name, Glyph) => {
    const { container } = render(() => <Glyph />);
    expect(container.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
  });

  // The muted marker on a voice tile is the only signal that someone cannot be
  // heard, so it has to reach a screen reader.
  it('names the muted marker when the tile labels it', () => {
    const { container } = render(() => <MutedIcon label="Muted" />);
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('aria-label')).toBe('Muted');
    expect(svg?.getAttribute('role')).toBe('img');
  });
});
