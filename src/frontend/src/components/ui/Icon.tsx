import { splitProps } from 'solid-js';
import type { Component, JSX } from 'solid-js';

type LucideProps = JSX.SvgSVGAttributes<SVGSVGElement> & {
  size?: number | string;
  'stroke-width'?: number | string;
};

export interface IconProps extends JSX.SvgSVGAttributes<SVGSVGElement> {
  /** A lucide-solid icon component, e.g. `Hash` from 'lucide-solid'. */
  icon: Component<LucideProps>;
  size?: number | string;
  /** Accessible name. Omit for decorative icons (they become aria-hidden). */
  label?: string;
}

/**
 * The one way to render an icon in this app.
 *
 * The design spec calls for a single 1.5px-stroke set replacing the ad-hoc SVGs
 * scattered across the component tree, so weight and size are decided here
 * rather than per call site. Value-identical to the hub's primitive, because
 * the two apps are meant to read as one product.
 *
 * Decorative by default: an icon beside a label is noise to a screen reader,
 * so it only gets a name when a caller passes `label`.
 */
export function Icon(props: IconProps) {
  const [local, rest] = splitProps(props, ['icon', 'size', 'label']);
  return (
    <local.icon
      size={local.size ?? 16}
      stroke-width={1.5}
      aria-hidden={local.label ? undefined : 'true'}
      aria-label={local.label}
      role={local.label ? 'img' : undefined}
      {...rest}
    />
  );
}

export default Icon;
