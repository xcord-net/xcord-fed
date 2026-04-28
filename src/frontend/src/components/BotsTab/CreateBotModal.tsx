import { For, Show } from 'solid-js';
import type { BotAgent } from './types';
import { ParamField } from './ParamField';
import styles from './CreateBotModal.module.css';

export interface CreateBotModalProps {
  agents: BotAgent[];
  username: string;
  displayName: string;
  tokenName: string;
  agentId: string;
  paramValues: Record<string, string>;
  isCreating: boolean;
  createError: string | null;
  newToken: string | null;
  copiedToken: boolean;

  onSetUsername: (v: string) => void;
  onSetDisplayName: (v: string) => void;
  onSetTokenName: (v: string) => void;
  onAgentChange: (agentId: string) => void;
  onSetParamValues: (
    updater: (prev: Record<string, string>) => Record<string, string>,
  ) => void;
  onSubmit: (e: Event) => void;
  onDismiss: () => void;
  onCopyToken: () => void;
}

export function CreateBotModal(props: CreateBotModalProps) {
  function selectedAgent(): BotAgent | null {
    if (!props.agentId) return null;
    return props.agents.find((a) => a.id === props.agentId) ?? null;
  }

  return (
    <div
      data-testid="create-bot-modal-backdrop"
      class={styles.modalBackdrop}
      onClick={(e) => {
        if (e.target === e.currentTarget && !props.newToken) props.onDismiss();
      }}
    >
      <div data-testid="create-bot-modal" class={styles.modalPanel}>
        <div class={styles.modalHeader}>
          <h3 class={styles.modalTitle}>
            {props.newToken ? 'Bot Created' : 'Create Bot'}
          </h3>
          <button
            data-testid="create-bot-modal-close"
            type="button"
            onClick={() => props.onDismiss()}
            class={styles.modalCloseButton}
          >
            &#10005;
          </button>
        </div>

        {/* Token reveal (post-create) */}
        <Show when={props.newToken}>
          <div class={styles.tokenRevealSection}>
            <div class={styles.tokenWarning}>
              This token will only be shown once. Copy it now before closing.
            </div>
            <div class={styles.tokenDisplay}>{props.newToken}</div>
            <button
              data-testid="create-bot-copy-token"
              type="button"
              onClick={() => props.onCopyToken()}
              class={styles.copyTokenButton}
            >
              {props.copiedToken ? 'Copied!' : 'Copy Token'}
            </button>
            <button
              data-testid="create-bot-done"
              type="button"
              onClick={() => props.onDismiss()}
              class={styles.doneButton}
            >
              Done
            </button>
          </div>
        </Show>

        {/* Create form */}
        <Show when={!props.newToken}>
          <form onSubmit={(e) => props.onSubmit(e)} class={styles.modalBody}>
            <div class={styles.fieldGroup}>
              <label for="bot-username" class={styles.fieldLabel}>
                Username <span class={styles.requiredMark}>*</span>
              </label>
              <input
                id="bot-username"
                data-testid="create-bot-username"
                type="text"
                required
                maxlength="32"
                value={props.username}
                onInput={(e) => props.onSetUsername(e.currentTarget.value)}
                class={styles.fieldInput}
                placeholder="my-bot"
              />
            </div>

            <div class={styles.fieldGroup}>
              <label for="bot-display-name" class={styles.fieldLabel}>
                Display Name <span class={styles.requiredMark}>*</span>
              </label>
              <input
                id="bot-display-name"
                data-testid="create-bot-display-name"
                type="text"
                required
                maxlength="80"
                value={props.displayName}
                onInput={(e) => props.onSetDisplayName(e.currentTarget.value)}
                class={styles.fieldInput}
                placeholder="My Bot"
              />
            </div>

            <div class={styles.fieldGroup}>
              <label for="bot-token-name" class={styles.fieldLabel}>
                Token Name <span class={styles.requiredMark}>*</span>
              </label>
              <input
                id="bot-token-name"
                data-testid="create-bot-token-name"
                type="text"
                required
                maxlength="80"
                value={props.tokenName}
                onInput={(e) => props.onSetTokenName(e.currentTarget.value)}
                class={styles.fieldInput}
                placeholder="Bot Token"
              />
            </div>

            <Show when={props.agents.length > 0}>
              <div class={styles.fieldGroup}>
                <label for="bot-agent" class={styles.fieldLabel}>
                  Agent (optional)
                </label>
                <select
                  id="bot-agent"
                  data-testid="create-bot-agent-select"
                  value={props.agentId}
                  onChange={(e) => props.onAgentChange(e.currentTarget.value)}
                  class={styles.fieldInput}
                >
                  <option value="">None</option>
                  <For each={props.agents}>
                    {(agent) => <option value={agent.id}>{agent.name}</option>}
                  </For>
                </select>
              </div>
            </Show>

            {/* Dynamic parameter fields */}
            <Show
              when={
                selectedAgent()?.manifest?.parameters &&
                selectedAgent()!.manifest!.parameters.length > 0
              }
            >
              <div class={styles.agentParamsBox}>
                <p class={styles.agentParamsBoxLabel}>Agent Parameters</p>
                <For each={selectedAgent()!.manifest!.parameters}>
                  {(param) => (
                    <ParamField
                      param={param}
                      value={props.paramValues[param.name] ?? ''}
                      onChange={(val) =>
                        props.onSetParamValues((prev) => ({ ...prev, [param.name]: val }))
                      }
                    />
                  )}
                </For>
              </div>
            </Show>

            <Show when={props.createError}>
              <div role="alert" class={styles.formAlertError}>
                {props.createError}
              </div>
            </Show>

            <div class={styles.modalFooter}>
              <button
                type="button"
                onClick={() => props.onDismiss()}
                class={styles.cancelButton}
              >
                Cancel
              </button>
              <button
                data-testid="create-bot-submit"
                type="submit"
                disabled={props.isCreating}
                class={styles.submitButton}
              >
                {props.isCreating ? 'Creating...' : 'Create Bot'}
              </button>
            </div>
          </form>
        </Show>
      </div>
    </div>
  );
}

export default CreateBotModal;
