import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import LayoutThumbnail from './LayoutThumbnail';

describe('LayoutThumbnail', () => {
  it('renders without crashing for the Grid preset', () => {
    const { container } = render(() => <LayoutThumbnail preset="Grid" />);
    expect(container.firstChild).not.toBeNull();
  });

  it('renders without crashing for each preset', () => {
    for (const preset of ['Grid', 'Spotlight', 'Pip', 'SideBySide'] as const) {
      const { container } = render(() => <LayoutThumbnail preset={preset} />);
      expect(container.firstChild).not.toBeNull();
    }
  });

  it('marks the thumbnail decorative with aria-hidden', () => {
    const { container } = render(() => <LayoutThumbnail preset="Grid" />);
    const wrap = container.firstElementChild as HTMLElement;
    expect(wrap.getAttribute('aria-hidden')).toBe('true');
  });
});
