import { For, Show } from 'solid-js';
import type { Bot, BotAgent } from './types';
import { ParamField } from './ParamField';
import Flexbox from '../ui/Flexbox';
import styles from './ConfigureBotModal.module.css';

export interface ConfigureBotModalProps {
  bot: Bot;
  agent: BotAgent | null;
  paramValues: Record<string, string>;
  isSaving: boolean;
  configError: string | null;
  configSuccess: string | null;
  onSetParamValues: (
    updater: (prev: Record<string, string>) => Record<string, string>,
  ) => void;
  onSave: (e: Event) => void;
  onClose: () => void;
}

export function ConfigureBotModal(props: ConfigureBotModalProps) {
  return (
    <Flexbox
      align="center"
      justify="center"
      data-testid="configure-bot-modal-backdrop"
      class={styles.modalBackdrop}
      onClick={(e) => {
        if (e.target === e.currentTarget) props.onClose();
      }}
    >
      <div data-testid="configure-bot-modal" class={styles.modalPanel}>
        <Flexbox align="center" justify="between" class={styles.modalHeader}>
          <h3 class={styles.modalTitle}>Configure {props.bot.displayName}</h3>
          <button
            data-testid="configure-bot-modal-close"
            type="button"
            onClick={() => props.onClose()}
            class={styles.modalCloseButton}
          >
            &#10005;
          </button>
        </Flexbox>

        <form onSubmit={(e) => props.onSave(e)} class={styles.modalBody}>
          <Show
            when={
              props.agent?.manifest?.parameters &&
              props.agent.manifest.parameters.length > 0
            }
            fallback={
              <p class={styles.noParamsText}>
                This agent has no configurable parameters.
              </p>
            }
          >
            <For each={props.agent!.manifest!.parameters}>
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
          </Show>

          <Show when={props.configError}>
            <div role="alert" class={styles.formAlertError}>
              {props.configError}
            </div>
          </Show>

          <Show when={props.configSuccess}>
            <div role="status" class={styles.formAlertSuccess}>
              {props.configSuccess}
            </div>
          </Show>

          <Flexbox justify="end" gap={0.75} class={styles.modalFooter}>
            <button
              type="button"
              onClick={() => props.onClose()}
              class={styles.cancelButton}
            >
              Close
            </button>
            <button
              data-testid="configure-bot-save"
              type="submit"
              disabled={props.isSaving}
              class={styles.submitButton}
            >
              {props.isSaving ? 'Saving...' : 'Save'}
            </button>
          </Flexbox>
        </form>
      </div>
    </Flexbox>
  );
}

export default ConfigureBotModal;
