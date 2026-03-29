import type { JSX } from 'solid-js';
import { splitProps } from 'solid-js';

type Direction = 'horizontal' | 'vertical';
type Align = 'start' | 'center' | 'end' | 'stretch' | 'baseline';
type Justify = 'start' | 'center' | 'end' | 'between' | 'around' | 'evenly';
type Wrap = 'wrap' | 'nowrap' | 'wrap-reverse';

const justifyMap: Record<Justify, string> = {
  start: 'flex-start',
  center: 'center',
  end: 'flex-end',
  between: 'space-between',
  around: 'space-around',
  evenly: 'space-evenly',
};

const alignMap: Record<Align, string> = {
  start: 'flex-start',
  center: 'center',
  end: 'flex-end',
  stretch: 'stretch',
  baseline: 'baseline',
};

interface FlexboxProps extends JSX.HTMLAttributes<HTMLDivElement> {
  direction?: Direction;
  align?: Align;
  justify?: Justify;
  gap?: string | number;
  wrap?: Wrap;
  inline?: boolean;
  children: JSX.Element;
}

export default function Flexbox(props: FlexboxProps) {
  const [local, rest] = splitProps(props, [
    'direction',
    'align',
    'justify',
    'gap',
    'wrap',
    'inline',
    'children',
    'style',
  ]);

  const computedStyle = (): JSX.CSSProperties => {
    const s: JSX.CSSProperties = {
      display: local.inline ? 'inline-flex' : 'flex',
    };
    if (local.direction) s['flex-direction'] = local.direction === 'vertical' ? 'column' : 'row';
    if (local.align) s['align-items'] = alignMap[local.align];
    if (local.justify) s['justify-content'] = justifyMap[local.justify];
    if (local.gap != null) s.gap = typeof local.gap === 'number' ? `${local.gap}rem` : local.gap;
    if (local.wrap) s['flex-wrap'] = local.wrap;

    // Merge any inline style passed via props
    if (typeof local.style === 'object' && local.style) {
      Object.assign(s, local.style);
    }

    return s;
  };

  return (
    <div {...rest} style={computedStyle()}>
      {local.children}
    </div>
  );
}

interface ItemProps extends JSX.HTMLAttributes<HTMLDivElement> {
  grow?: number | boolean;
  shrink?: number;
  basis?: string;
  align?: Align;
  children: JSX.Element;
}

function Item(props: ItemProps) {
  const [local, rest] = splitProps(props, [
    'grow',
    'shrink',
    'basis',
    'align',
    'children',
    'style',
  ]);

  const computedStyle = (): JSX.CSSProperties => {
    const s: JSX.CSSProperties = {};
    if (local.grow != null) s['flex-grow'] = local.grow === true ? '1' : String(local.grow);
    if (local.shrink != null) s['flex-shrink'] = String(local.shrink);
    if (local.basis) s['flex-basis'] = local.basis;
    if (local.align) s['align-self'] = alignMap[local.align];

    if (typeof local.style === 'object' && local.style) {
      Object.assign(s, local.style);
    }

    return s;
  };

  return (
    <div {...rest} style={computedStyle()}>
      {local.children}
    </div>
  );
}

Flexbox.Item = Item;
