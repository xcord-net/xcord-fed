import { For, Show, createSignal, createEffect, onCleanup } from 'solid-js';
import { api } from '../api/client';
import styles from './CommandPalette.module.css';

// ---- Types ----

export interface CommandParameter {
  name: string;
  description: string;
  required: boolean;
  type: 'string' | 'integer' | 'boolean' | 'user' | 'channel' | 'group';
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
      class={styles.palette}
      role="dialog"
      aria-label="Command palette"
    >
      {/* Header */}
      <div class={styles.paletteHeader}>
        <span class={styles.paletteHeaderLabel}>
          Slash Commands
        </span>
        <button
          class={styles.dismissBtn}
          onClick={props.onDismiss}
          aria-label="Close command palette"
        >
          ESC
        </button>
      </div>

      {/* Loading */}
      <Show when={isLoading()}>
        <div class={styles.loadingState}>
          <div class={styles.spinner} />
        </div>
      </Show>

      {/* Arg entry form */}
      <Show when={selectedCommand()}>
        {(cmd) => (
          <div class={styles.argForm}>
            <div class={styles.argFormHeader}>
              <span class={styles.cmdName}>/{cmd().name}</span>
              <span class={styles.cmdDescription}>{cmd().description}</span>
            </div>

            <div class={styles.paramList}>
              <For each={cmd().parameters}>
                {(param) => (
                  <div>
                    <label class={styles.paramLabel}>
                      {param.name}
                      <Show when={param.required}>
                        <span class={styles.requiredStar}>*</span>
                      </Show>
                      <span class={styles.paramHint}>{param.description}</span>
                    </label>
                    <input
                      type="text"
                      class={styles.paramInput}
                      placeholder={param.required ? `Required` : `Optional`}
                      value={args()[param.name] ?? ''}
                      onInput={(e) => handleArgInput(param.name, e.currentTarget.value)}
                    />
                  </div>
                )}
              </For>
            </div>

            <Show when={argError()}>
              <p class={styles.argError}>{argError()}</p>
            </Show>

            <div class={styles.argActions}>
              <button
                class={styles.sendBtn}
                onClick={handleSubmitArgs}
              >
                Send Command
              </button>
              <button
                class={styles.backBtn}
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
          <div class={styles.emptyState}>
            No commands found
          </div>
        </Show>

        <Show when={filtered().length > 0}>
          <ul
            class={styles.commandList}
            role="listbox"
            aria-label="Available commands"
          >
            <For each={filtered()}>
              {(cmd, index) => (
                <li
                  role="option"
                  aria-selected={selectedIndex() === index()}
                  class={`${styles.commandItem} ${
                    selectedIndex() === index()
                      ? styles.commandItemActive
                      : styles.commandItemInactive
                  }`}
                  onClick={() => handleCommandSelect(cmd)}
                  onMouseEnter={() => setSelectedIndex(index())}
                >
                  <div class={styles.commandHeader}>
                    <span class={styles.commandName}>
                      /{cmd.name}
                    </span>
                    <span class={styles.commandBot}>{cmd.botName}</span>
                  </div>
                  <p class={styles.commandDesc}>{cmd.description}</p>
                  <Show when={cmd.parameters.length > 0}>
                    <p class={styles.commandPreview}>
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
