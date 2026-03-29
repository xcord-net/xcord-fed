import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import { useChannels } from '../stores/channel.store';
import ChannelPermissions from './ChannelPermissions';
import Modal from './ui/Modal';
import { getErrorMessage } from '../utils/errors';
import styles from './ChannelSettings.module.css';

interface ChannelSettingsProps {
  serverId: string;
  channelId: string;
  onClose: () => void;
}

const SLOWMODE_OPTIONS: { label: string; value: number }[] = [
  { label: 'Off', value: 0 },
  { label: '5 seconds', value: 5 },
  { label: '10 seconds', value: 10 },
  { label: '15 seconds', value: 15 },
  { label: '30 seconds', value: 30 },
  { label: '1 minute', value: 60 },
  { label: '2 minutes', value: 120 },
  { label: '5 minutes', value: 300 },
  { label: '10 minutes', value: 600 },
  { label: '15 minutes', value: 900 },
  { label: '30 minutes', value: 1800 },
  { label: '1 hour', value: 3600 },
  { label: '2 hours', value: 7200 },
  { label: '6 hours', value: 21600 },
];

export default function ChannelSettings(props: ChannelSettingsProps) {
  const channelStore = useChannels();

  const currentChannel = () => channelStore.channels.find((c) => c.id === props.channelId);

  const [channelName, setChannelName] = createSignal('');
  const [topic, setTopic] = createSignal('');
  const [slowMode, setSlowMode] = createSignal(0);
  const [isNsfw, setIsNsfw] = createSignal(false);
  const [isSaving, setIsSaving] = createSignal(false);
  const [successMsg, setSuccessMsg] = createSignal('');
  const [errorMsg, setErrorMsg] = createSignal('');
  const [activeTab, setActiveTab] = createSignal<'overview' | 'permissions'>('overview');
  const [showDeleteConfirm, setShowDeleteConfirm] = createSignal(false);

  onMount(() => {
    const channel = currentChannel();
    if (channel) {
      setChannelName(channel.name);
      setTopic(channel.topic ?? '');
      setSlowMode(channel.slowModeSeconds);
      setIsNsfw(channel.isNsfw);
    }
  });

  const handleSave = async (e: Event) => {
    e.preventDefault();
    setIsSaving(true);
    setSuccessMsg('');
    setErrorMsg('');

    try {
      await api.patch(`/api/v1/channels/${props.channelId}`, {
        name: channelName().trim(),
        topic: topic().trim() || null,
        slowModeSeconds: slowMode(),
        isNsfw: isNsfw(),
      });
      channelStore.updateChannel(props.channelId, {
        name: channelName().trim(),
        topic: topic().trim() || undefined,
        slowModeSeconds: slowMode(),
        isNsfw: isNsfw(),
      });
      setSuccessMsg('Channel settings saved successfully.');
    } catch (err: unknown) {
      setErrorMsg(getErrorMessage(err, 'Failed to save channel settings.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <>
      <Modal data-testid="channel-settings-dialog" open={true} onClose={props.onClose} aria-label="Channel Settings" size="lg">
        {/* Header */}
        <div class={styles.header}>
          <div class={styles.headerText}>
            <h2 class={styles.headerTitle}>Channel Settings</h2>
            <p class={styles.headerSubtitle}>
              #{currentChannel()?.name ?? props.channelId}
            </p>
          </div>
          <button
            data-testid="channel-settings-close-button"
            type="button"
            aria-label="Close channel settings"
            onClick={props.onClose}
            class={styles.closeButton}
          >
            &#10005;
          </button>
        </div>

        {/* Tab navigation */}
        <div class={styles.tabNav}>
          <button
            data-testid="channel-settings-tab-overview"
            type="button"
            class={`${styles.tab} ${activeTab() === 'overview' ? styles.tabActive : ''}`}
            onClick={() => setActiveTab('overview')}
          >
            Overview
          </button>
          <button
            data-testid="channel-settings-tab-permissions"
            type="button"
            aria-label="Channel Permissions tab"
            class={`${styles.tab} ${activeTab() === 'permissions' ? styles.tabActive : ''}`}
            onClick={() => setActiveTab('permissions')}
          >
            Permissions
          </button>
        </div>

        {/* Body */}
        <Show when={activeTab() === 'permissions'}>
          <div class={styles.permissionsPanel}>
            <ChannelPermissions serverId={props.serverId} channelId={props.channelId} />
          </div>
        </Show>
        <Show when={activeTab() === 'overview'}>
          <form onSubmit={handleSave}>
            {/* Overview section */}
            <section class={styles.section}>
              <h3 class={styles.sectionHeading}>Overview</h3>

              {/* Channel name */}
              <div class={styles.fieldGroup}>
                <label for="channel-name" class={styles.fieldLabel}>
                  Channel Name <span class={styles.required}>*</span>
                </label>
                <div class={styles.inputWrapper}>
                  <span class={styles.inputPrefix} aria-hidden="true">#</span>
                  <input
                    id="channel-name"
                    type="text"
                    required
                    maxlength="100"
                    value={channelName()}
                    onInput={(e) => setChannelName(e.currentTarget.value)}
                    class={styles.textInputWithPrefix}
                    placeholder="channel-name"
                  />
                </div>
              </div>

              {/* Topic */}
              <div>
                <label for="channel-topic" class={styles.fieldLabel}>
                  Channel Topic
                </label>
                <textarea
                  id="channel-topic"
                  rows="3"
                  maxlength="1024"
                  value={topic()}
                  onInput={(e) => setTopic(e.currentTarget.value)}
                  class={styles.textarea}
                  placeholder="Let everyone know the purpose of this channel..."
                />
              </div>
            </section>

            {/* Permissions section */}
            <section class={styles.section}>
              <h3 class={styles.sectionHeading}>Permissions</h3>

              {/* Slowmode */}
              <div class={styles.slowmodeGroup}>
                <label for="slowmode" class={styles.fieldLabel}>
                  Slowmode
                </label>
                <select
                  id="slowmode"
                  value={String(slowMode())}
                  onChange={(e) => setSlowMode(parseInt(e.currentTarget.value, 10))}
                  class={styles.selectInput}
                >
                  <For each={SLOWMODE_OPTIONS}>
                    {(opt) => (
                      <option value={String(opt.value)}>{opt.label}</option>
                    )}
                  </For>
                </select>
                <p class={styles.fieldHint}>
                  Users must wait between sending messages.
                </p>
              </div>

              {/* NSFW toggle */}
              <div class={styles.toggleRow}>
                <div>
                  <label for="nsfw-toggle" class={styles.toggleLabel}>
                    Age-Restricted Channel (NSFW)
                  </label>
                  <p class={styles.toggleHint}>
                    Users must confirm they are 18+ to view this channel.
                  </p>
                </div>
                <button
                  type="button"
                  id="nsfw-toggle"
                  role="switch"
                  aria-checked={isNsfw()}
                  onClick={() => setIsNsfw(!isNsfw())}
                  class={`${styles.toggleButton} ${isNsfw() ? styles.toggleOn : styles.toggleOff}`}
                >
                  <span
                    class={`${styles.toggleThumb} ${isNsfw() ? styles.toggleThumbOn : styles.toggleThumbOff}`}
                  />
                </button>
              </div>
            </section>

            {/* Danger Zone */}
            <section class={styles.section}>
              <h3 class={styles.dangerHeading}>Danger Zone</h3>
              <button
                data-testid="delete-channel-button"
                type="button"
                class={styles.deleteChannelButton}
                onClick={() => setShowDeleteConfirm(true)}
              >
                Delete Channel
              </button>
            </section>

            {/* Status messages */}
            <Show when={successMsg()}>
              <div role="status" class={styles.successMsg}>
                {successMsg()}
              </div>
            </Show>
            <Show when={errorMsg()}>
              <div role="alert" class={styles.errorMsg}>
                {errorMsg()}
              </div>
            </Show>

            {/* Footer actions */}
            <div class={styles.footerActions}>
              <button
                type="button"
                onClick={props.onClose}
                class={styles.cancelButton}
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isSaving()}
                class={styles.saveButton}
              >
                {isSaving() ? 'Saving...' : 'Save Changes'}
              </button>
            </div>
          </form>
        </Show>
      </Modal>

      {/* Delete channel confirmation */}
      <Modal data-testid="delete-channel-dialog" open={showDeleteConfirm()} onClose={() => setShowDeleteConfirm(false)} title="Delete Channel" size="sm" role="alertdialog">
        <div class={styles.dialogBody}>
          <p class={styles.dialogText}>Are you sure you want to delete this channel? This cannot be undone.</p>
          <div class={styles.dialogActions}>
            <button
              data-testid="delete-channel-cancel-button"
              type="button"
              onClick={() => setShowDeleteConfirm(false)}
              class={styles.dialogCancelButton}
            >
              Cancel
            </button>
            <button
              data-testid="delete-channel-confirm-button"
              type="button"
              onClick={() => { channelStore.deleteChannel(props.channelId).then(() => props.onClose()); }}
              class={styles.dialogDeleteButton}
            >
              Delete Channel
            </button>
          </div>
        </div>
      </Modal>
    </>
  );
}
