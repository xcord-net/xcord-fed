import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import { Hash } from 'lucide-solid';
import { Icon } from './Icon';

describe('Icon', () => {
  it('renders the lucide icon it is given', () => {
    const { container } = render(() => <Icon icon={Hash} />);
    expect(container.querySelector('svg')).not.toBeNull();
  });

  it('uses the 1.5px stroke the design system specifies', () => {
    const { container } = render(() => <Icon icon={Hash} />);
    expect(container.querySelector('svg')?.getAttribute('stroke-width')).toBe('1.5');
  });

  it('defaults to 16px', () => {
    const { container } = render(() => <Icon icon={Hash} />);
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('width')).toBe('16');
    expect(svg?.getAttribute('height')).toBe('16');
  });

  it('honours an explicit size', () => {
    const { container } = render(() => <Icon icon={Hash} size={24} />);
    expect(container.querySelector('svg')?.getAttribute('width')).toBe('24');
  });

  it('is hidden from assistive tech when decorative', () => {
    const { container } = render(() => <Icon icon={Hash} />);
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('aria-hidden')).toBe('true');
    expect(svg?.getAttribute('role')).toBeNull();
  });

  it('becomes a named image when given a label', () => {
    const { container } = render(() => <Icon icon={Hash} label="Text channel" />);
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('aria-label')).toBe('Text channel');
    expect(svg?.getAttribute('role')).toBe('img');
    expect(svg?.getAttribute('aria-hidden')).toBeNull();
  });

  it('passes through class and data attributes', () => {
    const { container } = render(() => (
      <Icon icon={Hash} class="custom" data-testid="an-icon" />
    ));
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('class')).toContain('custom');
    expect(svg?.getAttribute('data-testid')).toBe('an-icon');
  });
});
