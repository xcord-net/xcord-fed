import { For, Show, Switch, Match, createSignal, createEffect, createMemo } from 'solid-js';
import { api } from '../api/client';
import styles from './ServerOnboarding.module.css';

// ---- Types ----

export interface OnboardingGroup {
  id: string;
  name: string;
  description?: string;
  emoji?: string;
}

export interface OnboardingChannel {
  channelId: string;
  channelName: string;
  description?: string;
}

export interface OnboardingConfig {
  serverId: string;
  promptMessage: string;
  rules: string;
  groups: OnboardingGroup[];
  channels: OnboardingChannel[];
  totalSteps: number;
}

export type OnboardingStep = 'rules' | 'groups' | 'channels' | 'complete';

interface ServerOnboardingProps {
  serverId: string;
  onComplete?: () => void;
}

// ---- Pure helpers ----

export function getOnboardingSteps(): OnboardingStep[] {
  return ['rules', 'groups', 'channels', 'complete'];
}

export function stepIndex(step: OnboardingStep): number {
  return getOnboardingSteps().indexOf(step);
}

export function nextStep(current: OnboardingStep): OnboardingStep | null {
  const steps = getOnboardingSteps();
  const idx = steps.indexOf(current);
  if (idx < 0 || idx >= steps.length - 1) return null;
  return steps[idx + 1];
}

export function previousStep(current: OnboardingStep): OnboardingStep | null {
  const steps = getOnboardingSteps();
  const idx = steps.indexOf(current);
  if (idx <= 0) return null;
  return steps[idx - 1];
}

export function stepLabel(step: OnboardingStep): string {
  switch (step) {
    case 'rules':
      return 'Rules';
    case 'groups':
      return 'Interests';
    case 'channels':
      return 'Channels';
    case 'complete':
      return 'Done';
  }
}

export function progressPercent(step: OnboardingStep): number {
  const steps = getOnboardingSteps();
  const idx = steps.indexOf(step);
  return Math.round((idx / (steps.length - 1)) * 100);
}

export function toggleGroupSelection(selectedIds: string[], groupId: string): string[] {
  return selectedIds.includes(groupId)
    ? selectedIds.filter((id) => id !== groupId)
    : [...selectedIds, groupId];
}

export function toggleChannelSelection(selectedIds: string[], channelId: string): string[] {
  return selectedIds.includes(channelId)
    ? selectedIds.filter((id) => id !== channelId)
    : [...selectedIds, channelId];
}

// ---- Component ----

export default function ServerOnboarding(props: ServerOnboardingProps) {
  const [config, setConfig] = createSignal<OnboardingConfig | null>(null);
  const [isLoading, setIsLoading] = createSignal(false);
  const [currentStep, setCurrentStep] = createSignal<OnboardingStep>('rules');
  const [rulesAccepted, setRulesAccepted] = createSignal(false);
  const [selectedGroupIds, setSelectedGroupIds] = createSignal<string[]>([]);
  const [selectedChannelIds, setSelectedChannelIds] = createSignal<string[]>([]);
  const [isSubmitting, setIsSubmitting] = createSignal(false);
  const [submitError, setSubmitError] = createSignal<string | null>(null);

  const loadConfig = async (serverId: string) => {
    setIsLoading(true);
    try {
      const data = await api.get<OnboardingConfig>(
        `/api/v1/servers/${serverId}/onboarding`,
      );
      setConfig(data);
    } catch {
      setConfig(null);
    } finally {
      setIsLoading(false);
    }
  };

  createEffect(() => {
    const serverId = props.serverId;
    if (serverId) {
      loadConfig(serverId);
    }
  });

  const canProceedFromRules = createMemo(() => rulesAccepted());

  const handleToggleGroup = (groupId: string) => {
    setSelectedGroupIds((prev) =>
      prev.includes(groupId) ? prev.filter((id) => id !== groupId) : [...prev, groupId],
    );
  };

  const handleToggleChannel = (channelId: string) => {
    setSelectedChannelIds((prev) =>
      prev.includes(channelId)
        ? prev.filter((id) => id !== channelId)
        : [...prev, channelId],
    );
  };

  const handleNext = () => {
    const next = nextStep(currentStep());
    if (next) {
      setCurrentStep(next);
      setSubmitError(null);
    }
  };

  const handleBack = () => {
    const prev = previousStep(currentStep());
    if (prev) {
      setCurrentStep(prev);
      setSubmitError(null);
    }
  };

  const handleComplete = async () => {
    setIsSubmitting(true);
    setSubmitError(null);
    try {
      await api.post(`/api/v1/servers/${props.serverId}/onboarding/complete`, {
        rulesAccepted: rulesAccepted(),
        selectedGroupIds: selectedGroupIds(),
        selectedChannelIds: selectedChannelIds(),
      });
      setCurrentStep('complete');
      props.onComplete?.();
    } catch {
      setSubmitError('Failed to complete onboarding. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const steps = getOnboardingSteps();
  const totalSteps = steps.length;

  return (
    <div class={styles.container}>
      <Show when={isLoading()}>
        <div class={styles.loadingCenter}>
          <div class={styles.spinner} />
        </div>
      </Show>

      <Show when={!isLoading() && config()}>
        <div class={styles.inner}>
          {/* Progress header */}
          <div class={styles.progressHeader}>
            <div class={styles.progressHeaderRow}>
              <h2 class={styles.progressTitle}>
                {config()!.promptMessage}
              </h2>
              <span class={styles.stepCounter}>
                Step {stepIndex(currentStep()) + 1} of {totalSteps}
              </span>
            </div>

            {/* Progress bar */}
            <div class={styles.progressTrack}>
              <div
                class={styles.progressFill}
                style={{ width: `${progressPercent(currentStep())}%` }}
                role="progressbar"
                aria-valuenow={progressPercent(currentStep())}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-label={`Onboarding progress: ${progressPercent(currentStep())}%`}
              />
            </div>

            {/* Step indicators */}
            <div class={styles.stepIndicators}>
              <For each={steps}>
                {(step) => (
                  <span
                    class={
                      step === currentStep()
                        ? `${styles.stepLabel} ${styles.stepLabelActive}`
                        : stepIndex(step) < stepIndex(currentStep())
                          ? `${styles.stepLabel} ${styles.stepLabelDone}`
                          : styles.stepLabel
                    }
                  >
                    {stepLabel(step)}
                  </span>
                )}
              </For>
            </div>
          </div>

          {/* Step content */}
          <div class={styles.stepContent}>
            <Switch>
              {/* Step 1: Rules */}
              <Match when={currentStep() === 'rules'}>
                <div class={styles.stepSection}>
                  <h3 class={styles.stepHeading}>Server Rules</h3>
                  <div class={styles.rulesBox}>
                    <p class={styles.rulesText}>
                      {config()!.rules}
                    </p>
                  </div>
                  <label class={styles.rulesLabel}>
                    <input
                      type="checkbox"
                      class={styles.rulesCheckbox}
                      checked={rulesAccepted()}
                      onChange={(e) => setRulesAccepted(e.currentTarget.checked)}
                      aria-label="I agree to the server rules"
                    />
                    <span class={styles.rulesCheckboxText}>
                      I have read and agree to the server rules
                    </span>
                  </label>
                </div>
              </Match>

              {/* Step 2: Groups / Interests */}
              <Match when={currentStep() === 'groups'}>
                <div class={styles.stepSection}>
                  <h3 class={styles.stepHeading}>Choose Your Interests</h3>
                  <p class={styles.stepDescription}>
                    Select groups that match your interests. You can change these later.
                  </p>
                  <div class={styles.groupGrid}>
                    <For each={config()!.groups}>
                      {(group) => {
                        const isSelected = () => selectedGroupIds().includes(group.id);
                        return (
                          <button
                            class={isSelected() ? `${styles.groupBtn} ${styles.groupBtnSelected}` : styles.groupBtn}
                            onClick={() => handleToggleGroup(group.id)}
                            aria-pressed={isSelected()}
                            aria-label={`Select interest: ${group.name}`}
                          >
                            <Show when={group.emoji}>
                              <span class={styles.groupEmoji}>{group.emoji}</span>
                            </Show>
                            <div class={styles.groupInfo}>
                              <p class={styles.groupName}>{group.name}</p>
                              <Show when={group.description}>
                                <p class={styles.groupDesc}>
                                  {group.description}
                                </p>
                              </Show>
                            </div>
                          </button>
                        );
                      }}
                    </For>
                  </div>
                  <Show when={config()!.groups.length === 0}>
                    <p class={styles.emptyText}>
                      No interest groups configured for this server.
                    </p>
                  </Show>
                </div>
              </Match>

              {/* Step 3: Channels */}
              <Match when={currentStep() === 'channels'}>
                <div class={styles.stepSection}>
                  <h3 class={styles.stepHeading}>Browse Channels</h3>
                  <p class={styles.stepDescription}>
                    Select channels you want to follow. You can always explore more later.
                  </p>
                  <div class={styles.channelList}>
                    <For each={config()!.channels}>
                      {(channel) => {
                        const isSelected = () => selectedChannelIds().includes(channel.channelId);
                        return (
                          <button
                            class={isSelected() ? `${styles.channelBtn} ${styles.channelBtnSelected}` : styles.channelBtn}
                            onClick={() => handleToggleChannel(channel.channelId)}
                            aria-pressed={isSelected()}
                            aria-label={`Select channel: ${channel.channelName}`}
                          >
                            <span class={styles.channelHash}>#</span>
                            <div class={styles.channelInfo}>
                              <p
                                class={isSelected() ? `${styles.channelName} ${styles.channelNameSelected}` : styles.channelName}
                              >
                                {channel.channelName}
                              </p>
                              <Show when={channel.description}>
                                <p class={styles.channelDesc}>
                                  {channel.description}
                                </p>
                              </Show>
                            </div>
                            <Show when={isSelected()}>
                              <span class={styles.channelCheck}>✓</span>
                            </Show>
                          </button>
                        );
                      }}
                    </For>
                  </div>
                  <Show when={config()!.channels.length === 0}>
                    <p class={styles.emptyText}>
                      No channels configured for this step.
                    </p>
                  </Show>
                </div>
              </Match>

              {/* Step 4: Complete */}
              <Match when={currentStep() === 'complete'}>
                <div class={styles.completeStep}>
                  <div class={styles.completeIcon}>
                    <span class={styles.completeIconText}>✓</span>
                  </div>
                  <h3 class={styles.completeTitle}>
                    You're all set!
                  </h3>
                  <p class={styles.completeSubtitle}>
                    Welcome to the server! You can explore channels and customize your preferences
                    anytime.
                  </p>
                </div>
              </Match>
            </Switch>
          </div>

          {/* Navigation footer */}
          <Show when={currentStep() !== 'complete'}>
            <div class={styles.navFooter}>
              <button
                class={styles.backBtn}
                onClick={handleBack}
                disabled={stepIndex(currentStep()) === 0}
                aria-label="Go back"
              >
                Back
              </button>

              <div class={styles.navRight}>
                <Show when={submitError()}>
                  <p class={styles.submitError} role="alert">
                    {submitError()}
                  </p>
                </Show>

                <Show when={currentStep() !== 'channels'}>
                  <button
                    class={styles.nextBtn}
                    onClick={handleNext}
                    disabled={currentStep() === 'rules' && !canProceedFromRules()}
                    aria-label="Next step"
                  >
                    Next
                  </button>
                </Show>

                <Show when={currentStep() === 'channels'}>
                  <button
                    class={styles.nextBtn}
                    onClick={handleComplete}
                    disabled={isSubmitting()}
                    aria-label="Complete onboarding"
                  >
                    {isSubmitting() ? 'Completing...' : 'Finish'}
                  </button>
                </Show>
              </div>
            </div>
          </Show>
        </div>
      </Show>

      <Show when={!isLoading() && !config()}>
        <div class={styles.noConfigCenter}>
          <p class={styles.noConfigText}>
            No onboarding configured for this server.
          </p>
        </div>
      </Show>
    </div>
  );
}

export { ServerOnboarding };
