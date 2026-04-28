import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import Flexbox from './Flexbox';

describe('Flexbox', () => {
  it('renders children inside a div with display:flex by default', () => {
    const { getByTestId } = render(() => (
      <Flexbox data-testid="fb">
        <span>Hello</span>
      </Flexbox>
    ));
    const el = getByTestId('fb');
    expect(el.tagName).toBe('DIV');
    expect(el.style.display).toBe('flex');
    expect(el.textContent).toBe('Hello');
  });

  it('uses inline-flex when inline prop is true', () => {
    const { getByTestId } = render(() => (
      <Flexbox inline data-testid="fb-inline">x</Flexbox>
    ));
    expect(getByTestId('fb-inline').style.display).toBe('inline-flex');
  });

  it('maps direction, align, justify, wrap, and gap to inline styles', () => {
    const { getByTestId } = render(() => (
      <Flexbox
        direction="vertical"
        align="center"
        justify="between"
        wrap="wrap"
        gap={0.5}
        data-testid="fb-styled"
      >
        <span>a</span>
      </Flexbox>
    ));
    const el = getByTestId('fb-styled');
    expect(el.style.flexDirection).toBe('column');
    expect(el.style.alignItems).toBe('center');
    expect(el.style.justifyContent).toBe('space-between');
    expect(el.style.flexWrap).toBe('wrap');
    expect(el.style.gap).toBe('0.5rem');
  });

  it('accepts string gap values verbatim', () => {
    const { getByTestId } = render(() => (
      <Flexbox gap="8px" data-testid="fb-gap">x</Flexbox>
    ));
    expect(getByTestId('fb-gap').style.gap).toBe('8px');
  });

  it('passes through arbitrary HTML props (class, role)', () => {
    const { getByTestId } = render(() => (
      <Flexbox class="custom" role="toolbar" data-testid="fb-pass">
        x
      </Flexbox>
    ));
    const el = getByTestId('fb-pass');
    expect(el.classList.contains('custom')).toBe(true);
    expect(el.getAttribute('role')).toBe('toolbar');
  });

  it('renders Flexbox.Item with grow/shrink/basis styles', () => {
    const { getByTestId } = render(() => (
      <Flexbox>
        <Flexbox.Item grow shrink={0} basis="10rem" data-testid="item">
          child
        </Flexbox.Item>
      </Flexbox>
    ));
    const el = getByTestId('item');
    expect(el.style.flexGrow).toBe('1');
    expect(el.style.flexShrink).toBe('0');
    expect(el.style.flexBasis).toBe('10rem');
  });
});
