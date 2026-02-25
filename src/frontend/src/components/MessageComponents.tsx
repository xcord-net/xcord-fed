import { For, Show, createSignal } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export type ButtonStyle = 'Primary' | 'Secondary' | 'Success' | 'Danger' | 'Link';

export interface MessageButton {
  customId: string;
  label: string;
  style: ButtonStyle;
  url?: string;
  disabled?: boolean;
  emoji?: string;
}

export interface SelectMenuOption {
  label: string;
  value: string;
  description?: string;
  emoji?: string;
}

export interface SelectMenu {
  customId: string;
  placeholder?: string;
  options: SelectMenuOption[];
  minValues?: number;
  maxValues?: number;
  disabled?: boolean;
}

export type ComponentType = 'button' | 'select_menu';

export interface ActionRow {
  type: 'action_row';
  components: (MessageButtonComponent | MessageSelectMenuComponent)[];
}

export interface MessageButtonComponent {
  type: 'button';
  button: MessageButton;
}

export interface MessageSelectMenuComponent {
  type: 'select_menu';
  menu: SelectMenu;
}

export interface InteractionResponse {
  interactionId: string;
  customId: string;
  values?: string[];
}

interface MessageComponentsProps {
  messageId: string;
  interactionId: string;
  actionRows: ActionRow[];
  onInteract?: (response: InteractionResponse) => void;
}

// ---- Helpers ----

export function buttonStyleClasses(style: ButtonStyle): string {
  switch (style) {
    case 'Primary':
      return 'bg-xcord-brand text-white hover:bg-xcord-brand-hover';
    case 'Secondary':
      return 'bg-xcord-bg-tertiary text-xcord-text-primary hover:bg-xcord-bg-primary';
    case 'Success':
      return 'bg-green-600 text-white hover:bg-green-700';
    case 'Danger':
      return 'bg-red-600 text-white hover:bg-red-700';
    case 'Link':
      return 'bg-transparent text-xcord-brand underline hover:text-xcord-brand/80';
    default:
      return 'bg-xcord-bg-tertiary text-xcord-text-primary hover:bg-xcord-bg-primary';
  }
}

export function isValidButtonStyle(style: string): style is ButtonStyle {
  return ['Primary', 'Secondary', 'Success', 'Danger', 'Link'].includes(style);
}

// ---- Button sub-component ----

function ComponentButton(props: {
  button: MessageButton;
  interactionId: string;
  onInteract?: (response: InteractionResponse) => void;
}) {
  const [loading, setLoading] = createSignal(false);

  const handleClick = async () => {
    if (props.button.disabled || loading()) return;

    if (props.button.style === 'Link' && props.button.url) {
      window.open(props.button.url, '_blank', 'noreferrer');
      return;
    }

    setLoading(true);
    try {
      const response: InteractionResponse = {
        interactionId: props.interactionId,
        customId: props.button.customId,
      };
      await api.post(`/api/v1/interactions/${props.interactionId}`, {
        customId: props.button.customId,
        type: 'button',
      });
      props.onInteract?.(response);
    } catch {
      // swallow — interaction errors are non-fatal
    } finally {
      setLoading(false);
    }
  };

  return (
    <button
      class={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed ${buttonStyleClasses(props.button.style)}`}
      disabled={props.button.disabled || loading()}
      onClick={handleClick}
      aria-label={props.button.label}
    >
      <Show when={loading()}>
        <span class="w-3 h-3 border border-current border-t-transparent rounded-full animate-spin" />
      </Show>
      <Show when={props.button.emoji}>
        <span aria-hidden="true">{props.button.emoji}</span>
      </Show>
      {props.button.label}
    </button>
  );
}

// ---- Select menu sub-component ----

function ComponentSelectMenu(props: {
  menu: SelectMenu;
  interactionId: string;
  onInteract?: (response: InteractionResponse) => void;
}) {
  const [selected, setSelected] = createSignal<string[]>([]);
  const [loading, setLoading] = createSignal(false);

  const minValues = () => props.menu.minValues ?? 1;
  const maxValues = () => props.menu.maxValues ?? 1;
  const isMulti = () => maxValues() > 1;

  const handleChange = async (e: Event) => {
    const select = e.target as HTMLSelectElement;
    const values = Array.from(select.selectedOptions).map((o) => o.value);
    setSelected(values);

    if (!isMulti()) {
      await submitInteraction(values);
    }
  };

  const submitInteraction = async (values: string[]) => {
    if (props.menu.disabled || loading()) return;
    setLoading(true);
    try {
      const response: InteractionResponse = {
        interactionId: props.interactionId,
        customId: props.menu.customId,
        values,
      };
      await api.post(`/api/v1/interactions/${props.interactionId}`, {
        customId: props.menu.customId,
        type: 'select_menu',
        values,
      });
      props.onInteract?.(response);
    } catch {
      // swallow
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="flex items-center gap-2">
      <select
        class="bg-xcord-bg-primary text-xcord-text-primary text-sm rounded px-3 py-1.5 outline-none focus:ring-1 focus:ring-xcord-brand disabled:opacity-50 disabled:cursor-not-allowed min-w-[180px]"
        multiple={isMulti()}
        disabled={props.menu.disabled || loading()}
        onChange={handleChange}
        aria-label={props.menu.placeholder ?? 'Select an option'}
      >
        <Show when={!isMulti() && props.menu.placeholder}>
          <option value="" disabled selected>
            {props.menu.placeholder}
          </option>
        </Show>
        <For each={props.menu.options}>
          {(opt) => (
            <option
              value={opt.value}
              selected={selected().includes(opt.value)}
              title={opt.description}
            >
              {opt.emoji ? `${opt.emoji} ` : ''}{opt.label}
            </option>
          )}
        </For>
      </select>

      <Show when={isMulti() && selected().length >= minValues()}>
        <button
          class="px-3 py-1.5 bg-xcord-brand text-white text-sm rounded hover:bg-xcord-brand-hover transition-colors disabled:opacity-50"
          disabled={loading()}
          onClick={() => submitInteraction(selected())}
        >
          Confirm
        </button>
      </Show>

      <Show when={loading()}>
        <span class="w-3 h-3 border border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
      </Show>
    </div>
  );
}

// ---- Main component ----

export default function MessageComponents(props: MessageComponentsProps) {
  return (
    <div class="mt-2 space-y-2" aria-label="Message components">
      <For each={props.actionRows}>
        {(row) => (
          <div class="flex flex-wrap items-center gap-2">
            <For each={row.components}>
              {(component) => (
                <Show when={component.type === 'button'}>
                  <ComponentButton
                    button={(component as MessageButtonComponent).button}
                    interactionId={props.interactionId}
                    onInteract={props.onInteract}
                  />
                </Show>
              )}
            </For>
            <For each={row.components}>
              {(component) => (
                <Show when={component.type === 'select_menu'}>
                  <ComponentSelectMenu
                    menu={(component as MessageSelectMenuComponent).menu}
                    interactionId={props.interactionId}
                    onInteract={props.onInteract}
                  />
                </Show>
              )}
            </For>
          </div>
        )}
      </For>
    </div>
  );
}

export { ComponentButton, ComponentSelectMenu };
