import { For, Show, createSignal, onMount } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { api } from '../api/client';
import { useServers } from '../stores/server.store';
import { useChannels } from '../stores/channel.store';
import { useAuth } from '../stores/auth.store';
import type { Channel } from '../types/channel';
import AutomodManager from './AutomodManager';
import BanManager from './BanManager';
import AuditLogViewer from './AuditLogViewer';
import EmojiManager from './EmojiManager';
import StickerPicker from './StickerPicker';
import VanityInvite from './VanityInvite';
import ServerTemplates from './ServerTemplates';
import ServerBoost from './ServerBoost';
import ServerInsights from './ServerInsights';
import InviteManager from './InviteManager';
import AppDirectory from './AppDirectory';
import BotsTab from './BotsTab';
import OwnershipTransfer from './OwnershipTransfer';
import WelcomeScreen from './WelcomeScreen';
import UpdatesTab from './UpdatesTab';
import Modal from './ui/Modal';
import { getErrorMessage } from '../utils/errors';
import styles from './ServerSettings.module.css';

interface ServerSettingsProps {
  serverId: string;
  onClose: () => void;
  initialTab?: SettingsTab;
}

type NotificationLevel = 'AllMessages' | 'OnlyMentions' | 'Nothing';
type SettingsTab = 'overview' | 'automod' | 'bans' | 'audit-log' | 'emoji' | 'stickers' | 'vanity-url' | 'templates' | 'boost' | 'insights' | 'invites' | 'app-directory' | 'bots' | 'welcome-screen' | 'updates';

const NOTIFICATION_OPTIONS: { label: string; value: NotificationLevel }[] = [
  { label: 'All Messages', value: 'AllMessages' },
  { label: 'Only @Mentions', value: 'OnlyMentions' },
  { label: 'Nothing', value: 'Nothing' },
];

const TABS: { id: SettingsTab; label: string; ownerOnly?: boolean }[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'automod', label: 'Automod', ownerOnly: true },
  { id: 'bans', label: 'Bans', ownerOnly: true },
  { id: 'audit-log', label: 'Audit Log', ownerOnly: true },
  { id: 'emoji', label: 'Emoji' },
  { id: 'stickers', label: 'Stickers' },
  { id: 'vanity-url', label: 'Vanity URL', ownerOnly: true },
  { id: 'templates', label: 'Templates', ownerOnly: true },
  { id: 'boost', label: 'Boost' },
  { id: 'insights', label: 'Insights', ownerOnly: true },
  { id: 'invites', label: 'Invites', ownerOnly: true },
  { id: 'app-directory', label: 'App Directory' },
  { id: 'bots', label: 'Bots', ownerOnly: true },
  { id: 'welcome-screen', label: 'Welcome Screen' },
  { id: 'updates', label: 'Updates', ownerOnly: true },
];

export default function ServerSettings(props: ServerSettingsProps) {
  const navigate = useNavigate();
  const serverStore = useServers();
  const channelStore = useChannels();
  const auth = useAuth();

  const currentServer = () => serverStore.servers.find((s) => s.id === props.serverId);
  const textChannels = () => channelStore.channels.filter((c) => c.type === 'Text');
  const isOwner = () => {
    const server = currentServer();
    const userId = auth.user?.id;
    return !!server && !!userId && server.ownerId === userId;
  };
  const visibleTabs = () => TABS.filter((tab) => !tab.ownerOnly || isOwner());

  const [activeTab, setActiveTab] = createSignal<SettingsTab>(props.initialTab ?? 'overview');
  const [name, setName] = createSignal('');
  const [description, setDescription] = createSignal('');
  const [systemChannelId, setSystemChannelId] = createSignal<string>('');
  const [defaultNotifications, setDefaultNotifications] = createSignal<NotificationLevel>('AllMessages');
  const [isSaving, setIsSaving] = createSignal(false);
  const [successMsg, setSuccessMsg] = createSignal('');
  const [errorMsg, setErrorMsg] = createSignal('');
  const [showDeleteConfirm, setShowDeleteConfirm] = createSignal(false);

  onMount(async () => {
    // Ensure servers are loaded so isOwner() resolves correctly before
    // rendering owner-only tabs (Invites, Vanity URL, Templates, etc.).
    if (serverStore.servers.length === 0) {
      await serverStore.fetchServers();
    }
    const server = currentServer();
    if (server) {
      setName(server.name);
      setDescription(server.description ?? '');
    }
  });

  const handleSave = async (e: Event) => {
    e.preventDefault();
    setIsSaving(true);
    setSuccessMsg('');
    setErrorMsg('');

    try {
      await api.patch(`/api/v1/servers/${props.serverId}`, {
        name: name().trim(),
        description: description().trim() || null,
        systemChannelId: systemChannelId() || null,
        defaultNotificationLevel: defaultNotifications(),
      });
      serverStore.updateServer(props.serverId, {
        name: name().trim(),
        description: description().trim() || undefined,
      });
      setSuccessMsg('Server settings saved successfully.');
    } catch (err: unknown) {
      setErrorMsg(getErrorMessage(err, 'Failed to save settings.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <>
      <Modal data-testid="server-settings-dialog" open={true} onClose={props.onClose} aria-label="Server Settings" size="xl">
        {/* Header */}
        <div class={styles.header}>
          <h2 class={styles.headerTitle}>Server Settings</h2>
          <button
            data-testid="server-settings-close-button"
            type="button"
            aria-label="Close settings"
            onClick={props.onClose}
            class={styles.closeButton}
          >
            &#10005;
          </button>
        </div>

        {/* Tab navigation */}
        <div class={styles.tabNav}>
          <For each={visibleTabs()}>
            {(tab) => (
              <button
                data-testid={`server-settings-tab-${tab.id}`}
                type="button"
                class={`${styles.tab} ${activeTab() === tab.id ? styles.tabActive : ''}`}
                onClick={() => setActiveTab(tab.id)}
              >
                {tab.label}
              </button>
            )}
          </For>
        </div>

        {/* Body */}
        {/* Overview tab */}
        <Show when={activeTab() === 'overview'}>
          <form onSubmit={handleSave}>
            {/* Overview section */}
            <section class={styles.section}>
              <h3 class={styles.sectionHeading}>Overview</h3>

              {/* Server icon placeholder */}
              <div class={styles.iconRow}>
                <div
                  class={styles.serverIcon}
                  aria-label="Server icon"
                >
                  {name() ? name().charAt(0).toUpperCase() : '?'}
                </div>
                <div>
                  <p class={styles.iconName}>{name() || currentServer()?.name}</p>
                  <p class={styles.iconHint}>Icon upload coming soon</p>
                </div>
              </div>

              {/* Server name */}
              <div class={styles.fieldGroup}>
                <label for="server-name" class={styles.fieldLabel}>
                  Server Name <span class={styles.required}>*</span>
                </label>
                <input
                  id="server-name"
                  data-testid="server-name-input"
                  type="text"
                  required
                  maxlength="100"
                  value={name()}
                  onInput={(e) => setName(e.currentTarget.value)}
                  class={styles.textInput}
                  placeholder="My Awesome Server"
                />
              </div>

              {/* Description */}
              <div>
                <label for="server-description" class={styles.fieldLabel}>
                  Description
                </label>
                <textarea
                  id="server-description"
                  rows="3"
                  maxlength="1000"
                  value={description()}
                  onInput={(e) => setDescription(e.currentTarget.value)}
                  class={styles.textarea}
                  placeholder="Tell people what your server is about..."
                />
              </div>
            </section>

            {/* System Messages section */}
            <section class={styles.section}>
              <h3 class={styles.sectionHeading}>System Messages</h3>

              <div>
                <label for="system-channel" class={styles.fieldLabel}>
                  System Messages Channel
                </label>
                <select
                  id="system-channel"
                  value={systemChannelId()}
                  onChange={(e) => setSystemChannelId(e.currentTarget.value)}
                  class={styles.selectInput}
                >
                  <option value="">None</option>
                  <For each={textChannels()}>
                    {(channel: Channel) => (
                      <option value={channel.id}>#{channel.name}</option>
                    )}
                  </For>
                </select>
                <p class={styles.fieldHint}>
                  Channel where join/leave and server boost messages are sent.
                </p>
              </div>
            </section>

            {/* Default Notifications section */}
            <section class={styles.section}>
              <h3 class={styles.sectionHeading}>Default Notification Settings</h3>

              <div>
                <label for="default-notifications" class={styles.fieldLabel}>
                  Default Notification Level
                </label>
                <select
                  id="default-notifications"
                  value={defaultNotifications()}
                  onChange={(e) => setDefaultNotifications(e.currentTarget.value as NotificationLevel)}
                  class={styles.selectInput}
                >
                  <For each={NOTIFICATION_OPTIONS}>
                    {(opt) => (
                      <option value={opt.value}>{opt.label}</option>
                    )}
                  </For>
                </select>
                <p class={styles.fieldHint}>
                  Controls what notifications members receive by default.
                </p>
              </div>
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
                data-testid="server-settings-save-button"
                type="submit"
                disabled={isSaving()}
                class={styles.saveButton}
              >
                {isSaving() ? 'Saving...' : 'Save Changes'}
              </button>
            </div>
          </form>

          {/* Danger Zone */}
          <Show when={isOwner()}>
            <section class={styles.dangerSection}>
              <h3 class={styles.dangerHeading}>Danger Zone</h3>
              <Show when={auth.user?.id && currentServer()?.ownerId}>
                <OwnershipTransfer
                  serverId={props.serverId}
                  serverName={currentServer()?.name ?? ''}
                  currentUserId={auth.user!.id}
                  ownerId={currentServer()!.ownerId}
                  onTransferred={(newOwnerId) => {
                    serverStore.updateServer(props.serverId, { ownerId: newOwnerId });
                  }}
                />
              </Show>
              <button
                type="button"
                class={styles.deleteButton}
                onClick={() => setShowDeleteConfirm(true)}
              >
                Delete Server
              </button>
            </section>
          </Show>
        </Show>

        {/* Automod tab */}
        <Show when={activeTab() === 'automod'}>
          <AutomodManager serverId={props.serverId} />
        </Show>

        {/* Bans tab */}
        <Show when={activeTab() === 'bans'}>
          <BanManager serverId={props.serverId} />
        </Show>

        {/* Audit Log tab */}
        <Show when={activeTab() === 'audit-log'}>
          <AuditLogViewer serverId={props.serverId} />
        </Show>

        {/* Emoji tab */}
        <Show when={activeTab() === 'emoji'}>
          <EmojiManager serverId={props.serverId} />
        </Show>

        {/* Stickers tab */}
        <Show when={activeTab() === 'stickers'}>
          <StickerPicker serverId={props.serverId} canManage={true} />
        </Show>

        {/* Vanity URL tab */}
        <Show when={activeTab() === 'vanity-url'}>
          <div class={styles.tabPadding}>
            <VanityInvite serverId={props.serverId} isOwner={true} />
          </div>
        </Show>

        {/* Templates tab */}
        <Show when={activeTab() === 'templates'}>
          <ServerTemplates serverId={props.serverId} isOwner={true} />
        </Show>

        {/* Boost tab */}
        <Show when={activeTab() === 'boost'}>
          <ServerBoost serverId={props.serverId} />
        </Show>

        {/* Insights tab */}
        <Show when={activeTab() === 'insights'}>
          <ServerInsights serverId={props.serverId} />
        </Show>

        {/* App Directory tab */}
        <Show when={activeTab() === 'app-directory'}>
          <AppDirectory
            availableServerIds={[props.serverId]}
            serverNames={{ [props.serverId]: currentServer()?.name ?? props.serverId }}
          />
        </Show>

        {/* Bots tab */}
        <Show when={activeTab() === 'bots'}>
          <BotsTab />
        </Show>

        {/* Invites tab */}
        <Show when={activeTab() === 'invites'}>
          <InviteManager serverId={props.serverId} />
        </Show>

        {/* Welcome Screen tab */}
        <Show when={activeTab() === 'welcome-screen'}>
          <WelcomeScreen serverId={props.serverId} isOwner={isOwner()} />
        </Show>

        {/* Updates tab */}
        <Show when={activeTab() === 'updates'}>
          <div class={styles.tabPadding}>
            <UpdatesTab />
          </div>
        </Show>
      </Modal>

      {/* Delete server confirmation */}
      <Modal open={showDeleteConfirm()} onClose={() => setShowDeleteConfirm(false)} title="Delete Server" size="sm" role="alertdialog">
        <div class={styles.dialogBody}>
          <p class={styles.dialogText}>Are you sure you want to delete this server? This cannot be undone.</p>
          <div class={styles.dialogActions}>
            <button
              type="button"
              onClick={() => setShowDeleteConfirm(false)}
              class={styles.cancelButton}
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={() => {
                serverStore.deleteServer(props.serverId).then(() => {
                  props.onClose();
                  navigate('/channels/me');
                });
              }}
              class={styles.dialogDeleteButton}
            >
              Delete Server
            </button>
          </div>
        </div>
      </Modal>
    </>
  );
}
