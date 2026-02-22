import { For, Show, createSignal, createEffect, onCleanup } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export interface CommandParameter {
  name: string;
  description: string;
  required: boolean;
  type: 'string' | 'integer' | 'boolean' | 'user' | 'channel' | 'role';
}

export interface BotCommand {
  id: string;
  name: string;
  description: string;
  parameters: CommandParameter[];
  botId: string;
  botName: string;
}

interface CommandPaletteProps {
  serverId: string;
  onSelectCommand: (command: BotCommand, args: Record<string, string>) => void;
  onDismiss: () => void;
  filter?: string;
}

// ---- Helpers ----

export function filterCommands(commands: BotCommand[], query: string): BotCommand[] {
  const lower = query.toLowerCase().replace(/^\//, '');
  if (!lower) return commands;
  return commands.filter(
    (cmd) =>
      cmd.name.toLowerCase().includes(lower) ||
      cmd.description.toLowerCase().includes(lower) ||
      cmd.botName.toLowerCase().includes(lower),
  );
}

export function buildCommandPreview(command: BotCommand): string {
  const paramStr = command.parameters
    .map((p) => (p.required ? `<${p.name}>` : `[${p.name}]`))
    .join(' ');
  return paramStr ? `/${command.name} ${paramStr}` : `/${command.name}`;
}

export function validateCommandArgs(
  command: BotCommand,
  args: Record<string, string>,
): string | null {
  for (const param of command.parameters) {
    if (param.required && !args[param.name]?.trim()) {
      return `Parameter "${param.name}" is required.`;
    }
  }
  return null;
}

// ---- Component ----

export default function CommandPalette(props: CommandPaletteProps) {
  const [commands, setCommands] = createSignal<BotCommand[]>([]);
  const [isLoading, setIsLoading] = createSignal(false);
  const [selectedIndex, setSelectedIndex] = createSignal(0);
  const [selectedCommand, setSelectedCommand] = createSignal<BotCommand | null>(null);
  const [args, setArgs] = createSignal<Record<string, string>>({});
  const [argError, setArgError] = createSignal<string | null>(null);

  const filtered = () => filterCommands(commands(), props.filter ?? '');

  // Load commands on mount
  createEffect(() => {
    const serverId = props.serverId;
    if (!serverId) return;

    setIsLoading(true);
    api
      .get<BotCommand[]>(`/api/v1/servers/${serverId}/commands`)
      .then((data) => setCommands(data))
      .catch(() => setCommands([]))
      .finally(() => setIsLoading(false));
  });

  // Reset selection when filter changes
  createEffect(() => {
    void props.filter;
    setSelectedIndex(0);
    setSelectedCommand(null);
    setArgs({});
    setArgError(null);
  });

  const handleKeyDown = (e: KeyboardEvent) => {
    const list = filtered();
    if (selectedCommand()) {
      if (e.key === 'Escape') {
        setSelectedCommand(null);
        setArgs({});
        setArgError(null);
      }
      return;
    }

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setSelectedIndex((i) => Math.min(i + 1, list.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setSelectedIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const cmd = list[selectedIndex()];
      if (cmd) handleCommandSelect(cmd);
    } else if (e.key === 'Escape') {
      props.onDismiss();
    }
  };

  // Attach global keydown while palette is visible
  document.addEventListener('keydown', handleKeyDown);
  onCleanup(() => document.removeEventListener('keydown', handleKeyDown));

  const handleCommandSelect = (cmd: BotCommand) => {
    if (cmd.parameters.length === 0) {
      props.onSelectCommand(cmd, {});
    } else {
      setSelectedCommand(cmd);
      setArgs({});
      setArgError(null);
    }
  };

  const handleArgInput = (paramName: string, value: string) => {
    setArgs((prev) => ({ ...prev, [paramName]: value }));
    setArgError(null);
  };

  const handleSubmitArgs = () => {
    const cmd = selectedCommand();
    if (!cmd) return;
    const error = validateCommandArgs(cmd, args());
    if (error) {
      setArgError(error);
      return;
    }
    props.onSelectCommand(cmd, args());
  };

  return (
    <div
      class="absolute bottom-full left-0 w-full max-w-xl bg-xcord-bg-secondary border border-xcord-bg-primary rounded-lg shadow-xl mb-1 overflow-hidden"
      role="dialog"
      aria-label="Command palette"
    >
      {/* Header */}
      <div class="px-3 py-2 border-b border-xcord-bg-primary flex items-center justify-between">
        <span class="text-xcord-text-muted text-xs font-medium uppercase tracking-wide">
          Slash Commands
        </span>
        <button
          class="text-xcord-text-muted hover:text-xcord-text-primary text-xs transition-colors"
          onClick={props.onDismiss}
          aria-label="Close command palette"
        >
          ESC
        </button>
      </div>

      {/* Loading */}
      <Show when={isLoading()}>
        <div class="flex items-center justify-center py-6">
          <div class="w-4 h-4 border-2 border-xcord-text-muted border-t-transparent rounded-full animate-spin" />
        </div>
      </Show>

      {/* Arg entry form */}
      <Show when={selectedCommand()}>
        {(cmd) => (
          <div class="p-3 space-y-3">
            <div class="flex items-center gap-2">
              <span class="text-xcord-brand font-mono text-sm">/{cmd().name}</span>
              <span class="text-xcord-text-muted text-xs">{cmd().description}</span>
            </div>

            <div class="space-y-2">
              <For each={cmd().parameters}>
                {(param) => (
                  <div>
                    <label class="block text-xcord-text-muted text-xs font-medium mb-0.5">
                      {param.name}
                      <Show when={param.required}>
                        <span class="text-red-400 ml-0.5">*</span>
                      </Show>
                      <span class="ml-2 text-xcord-text-muted font-normal">{param.description}</span>
                    </label>
                    <input
                      type="text"
                      class="w-full bg-xcord-bg-primary text-xcord-text-primary placeholder-xcord-text-muted rounded px-3 py-1.5 text-sm outline-none focus:ring-1 focus:ring-xcord-brand"
                      placeholder={param.required ? `Required` : `Optional`}
                      value={args()[param.name] ?? ''}
                      onInput={(e) => handleArgInput(param.name, e.currentTarget.value)}
                    />
                  </div>
                )}
              </For>
            </div>

            <Show when={argError()}>
              <p class="text-red-400 text-xs">{argError()}</p>
            </Show>

            <div class="flex gap-2">
              <button
                class="px-3 py-1.5 bg-xcord-brand text-white text-sm rounded hover:bg-xcord-brand/80 transition-colors"
                onClick={handleSubmitArgs}
              >
                Send Command
              </button>
              <button
                class="px-3 py-1.5 bg-xcord-bg-primary text-xcord-text-muted text-sm rounded hover:text-xcord-text-primary transition-colors"
                onClick={() => {
                  setSelectedCommand(null);
                  setArgs({});
                  setArgError(null);
                }}
              >
                Back
              </button>
            </div>
          </div>
        )}
      </Show>

      {/* Command list */}
      <Show when={!isLoading() && !selectedCommand()}>
        <Show when={filtered().length === 0}>
          <div class="px-3 py-4 text-xcord-text-muted text-sm text-center">
            No commands found
          </div>
        </Show>

        <Show when={filtered().length > 0}>
          <ul
            class="max-h-64 overflow-y-auto divide-y divide-xcord-bg-primary"
            role="listbox"
            aria-label="Available commands"
          >
            <For each={filtered()}>
              {(cmd, index) => (
                <li
                  role="option"
                  aria-selected={selectedIndex() === index()}
                  class={`px-3 py-2.5 cursor-pointer transition-colors ${
                    selectedIndex() === index()
                      ? 'bg-xcord-bg-primary'
                      : 'hover:bg-xcord-bg-primary/50'
                  }`}
                  onClick={() => handleCommandSelect(cmd)}
                  onMouseEnter={() => setSelectedIndex(index())}
                >
                  <div class="flex items-baseline gap-2">
                    <span class="text-xcord-brand font-mono text-sm font-medium">
                      /{cmd.name}
                    </span>
                    <span class="text-xcord-text-muted text-xs">{cmd.botName}</span>
                  </div>
                  <p class="text-xcord-text-secondary text-xs mt-0.5">{cmd.description}</p>
                  <Show when={cmd.parameters.length > 0}>
                    <p class="text-xcord-text-muted text-xs font-mono mt-0.5">
                      {buildCommandPreview(cmd)}
                    </p>
                  </Show>
                </li>
              )}
            </For>
          </ul>
        </Show>
      </Show>
    </div>
  );
}
