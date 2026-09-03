import { For, Show, createSignal, onMount } from 'solid-js';
import {
  useStreambot,
  type StreamBot,
  type StreamBotPlatform,
} from '../stores/streambot.store';
import { getErrorMessage } from '../utils/errors';
import styles from './StreambotManager.module.css';
import EmptyState from './ui/EmptyState';

interface Props {
  channelId: string;
}

const PLATFORMS: { value: StreamBotPlatform; label: string; defaultRtmp: string }[] = [
  { value: 'YouTube', label: 'YouTube', defaultRtmp: 'rtmp://a.rtmp.youtube.com/live2' },
  { value: 'Twitch', label: 'Twitch', defaultRtmp: 'rtmp://live.twitch.tv/app' },
  { value: 'Rumble', label: 'Rumble', defaultRtmp: 'rtmp://live.rumble.com/live' },
  { value: 'Custom', label: 'Custom', defaultRtmp: '' },
];

function truncate(url: string, max = 42): string {
  if (url.length <= max) return url;
  return url.slice(0, max - 3) + '...';
}

export default function StreambotManager(props: Props) {
  const streambot = useStreambot();

  const [showForm, setShowForm] = createSignal(false);
  const [editingId, setEditingId] = createSignal<string | null>(null);
  const [name, setName] = createSignal('');
  const [platform, setPlatform] = createSignal<StreamBotPlatform>('YouTube');
  const [rtmpUrl, setRtmpUrl] = createSignal(PLATFORMS[0].defaultRtmp);
  const [streamKey, setStreamKey] = createSignal('');
  const [isDefault, setIsDefault] = createSignal(false);
  const [saving, setSaving] = createSignal(false);
  const [formError, setFormError] = createSignal<string | null>(null);
  const [testStatus, setTestStatus] = createSignal<Record<string, { ok: boolean; msg: string } | 'pending'>>({});

  onMount(() => {
    streambot.load(props.channelId).catch(() => {
      // Non-fatal; list will be empty.
    });
  });

  const bots = () => streambot.getForChannel(props.channelId);

  const resetForm = () => {
    setEditingId(null);
    setName('');
    setPlatform('YouTube');
    setRtmpUrl(PLATFORMS[0].defaultRtmp);
    setStreamKey('');
    setIsDefault(false);
    setFormError(null);
  };

  const openCreate = () => {
    resetForm();
    setShowForm(true);
  };

  const openEdit = (bot: StreamBot) => {
    setEditingId(bot.id);
    setName(bot.name);
    setPlatform(bot.platform);
    setRtmpUrl(bot.rtmpUrl);
    setStreamKey('');
    setIsDefault(bot.isDefault);
    setFormError(null);
    setShowForm(true);
  };

  const handlePlatformChange = (p: StreamBotPlatform) => {
    setPlatform(p);
    // When creating or when current URL matches a known default, swap to the
    // new platform's default to make setup easier.
    const current = rtmpUrl().trim();
    const isKnownDefault = PLATFORMS.some(pl => pl.defaultRtmp === current);
    if (!editingId() || isKnownDefault || current === '') {
      const next = PLATFORMS.find(pl => pl.value === p);
      setRtmpUrl(next?.defaultRtmp ?? '');
    }
  };

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setFormError(null);
    const trimmedName = name().trim();
    const trimmedUrl = rtmpUrl().trim();
    if (!trimmedName) {
      setFormError('Name is required.');
      return;
    }
    if (!trimmedUrl) {
      setFormError('RTMP URL is required.');
      return;
    }
    if (!editingId() && !streamKey().trim()) {
      setFormError('Stream key is required.');
      return;
    }
    setSaving(true);
    try {
      if (editingId()) {
        const patch: { name?: string; rtmpUrl?: string; streamKey?: string; isDefault?: boolean } = {
          name: trimmedName,
          rtmpUrl: trimmedUrl,
          isDefault: isDefault(),
        };
        const sk = streamKey().trim();
        if (sk) patch.streamKey = sk;
        await streambot.update(props.channelId, editingId()!, patch);
      } else {
        await streambot.create(props.channelId, {
          name: trimmedName,
          platform: platform(),
          rtmpUrl: trimmedUrl,
          streamKey: streamKey().trim(),
          isDefault: isDefault(),
        });
      }
      setShowForm(false);
      resetForm();
    } catch (err) {
      setFormError(getErrorMessage(err, 'Failed to save streambot.'));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (bot: StreamBot) => {
    if (!confirm(`Delete streambot "${bot.name}"?`)) return;
    try {
      await streambot.delete(props.channelId, bot.id);
    } catch {
      // Non-fatal.
    }
  };

  const handleTest = async (bot: StreamBot) => {
    setTestStatus(prev => ({ ...prev, [bot.id]: 'pending' }));
    try {
      const res = await streambot.test(bot.id);
      setTestStatus(prev => ({
        ...prev,
        [bot.id]: res.success
          ? { ok: true, msg: 'Reachable' }
          : { ok: false, msg: res.error ?? 'Test failed' },
      }));
    } catch (err) {
      setTestStatus(prev => ({
        ...prev,
        [bot.id]: { ok: false, msg: getErrorMessage(err, 'Test failed') },
      }));
    }
  };

  const cancelForm = () => {
    setShowForm(false);
    resetForm();
  };

  return (
    <div class={styles.panel} data-testid="streambot-manager">
      <div class={styles.header}>
        <div>
          <h3 class={styles.title}>Streambots</h3>
          <p class={styles.subtitle}>
            Restream broadcasts from this channel to external RTMP destinations.
          </p>
        </div>
        <Show when={!showForm()}>
          <button
            type="button"
            class={styles.addButton}
            onClick={openCreate}
            data-testid="streambot-add-button"
          >
            Add Streambot
          </button>
        </Show>
      </div>

      <Show when={showForm()}>
        <form onSubmit={handleSubmit} class={styles.form}>
          <h4 class={styles.formTitle}>{editingId() ? 'Edit Streambot' : 'New Streambot'}</h4>

          <div class={styles.field}>
            <label for="streambot-name" class={styles.label}>Name</label>
            <input
              id="streambot-name"
              type="text"
              value={name()}
              onInput={(e) => setName(e.currentTarget.value)}
              class={styles.input}
              placeholder="Main YouTube"
              data-testid="streambot-name-input"
              required
            />
          </div>

          <div class={styles.field}>
            <label for="streambot-platform" class={styles.label}>Platform</label>
            <select
              id="streambot-platform"
              value={platform()}
              onChange={(e) => handlePlatformChange(e.currentTarget.value as StreamBotPlatform)}
              class={styles.input}
              data-testid="streambot-platform-select"
              disabled={!!editingId()}
            >
              <For each={PLATFORMS}>
                {(p) => <option value={p.value}>{p.label}</option>}
              </For>
            </select>
          </div>

          <div class={styles.field}>
            <label for="streambot-rtmp" class={styles.label}>RTMP URL</label>
            <input
              id="streambot-rtmp"
              type="text"
              value={rtmpUrl()}
              onInput={(e) => setRtmpUrl(e.currentTarget.value)}
              class={styles.input}
              placeholder="rtmp://..."
              data-testid="streambot-rtmp-input"
              required
            />
          </div>

          <div class={styles.field}>
            <label for="streambot-key" class={styles.label}>
              Stream Key {editingId() ? '(leave blank to keep existing)' : ''}
            </label>
            <input
              id="streambot-key"
              type="password"
              value={streamKey()}
              onInput={(e) => setStreamKey(e.currentTarget.value)}
              class={styles.input}
              placeholder={editingId() ? 'Leave blank to keep existing key' : 'Required'}
              data-testid="streambot-key-input"
              autocomplete="off"
            />
          </div>

          <div class={styles.checkboxField}>
            <label class={styles.checkboxLabel}>
              <input
                type="checkbox"
                checked={isDefault()}
                onChange={(e) => setIsDefault(e.currentTarget.checked)}
                data-testid="streambot-default-checkbox"
              />
              Default (pre-selected when starting a broadcast)
            </label>
          </div>

          <Show when={formError()}>
            <div role="alert" class={styles.errorMsg}>{formError()}</div>
          </Show>

          <div class={styles.formActions}>
            <button
              type="button"
              onClick={cancelForm}
              class={styles.cancelButton}
              data-testid="streambot-cancel-button"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={saving()}
              class={styles.saveButton}
              data-testid="streambot-save-button"
            >
              {saving() ? 'Saving...' : editingId() ? 'Save Changes' : 'Create Streambot'}
            </button>
          </div>
        </form>
      </Show>

      <div class={styles.list}>
        <Show
          when={bots().length > 0}
          fallback={
            <EmptyState
              title="No streambots on this channel"
              body="A streambot forwards this channel's broadcast to somewhere outside Xcord."
              dense
              data-testid="streambot-manager-empty"
            />
          }
        >
          <For each={bots()}>
            {(bot) => {
              const status = () => testStatus()[bot.id];
              return (
                <div class={styles.botRow} data-testid={`streambot-row-${bot.id}`}>
                  <div class={styles.botMain}>
                    <div class={styles.botTitleRow}>
                      <span class={styles.botName}>{bot.name}</span>
                      <span class={styles.platformBadge}>{bot.platform}</span>
                      <Show when={bot.isDefault}>
                        <span class={styles.defaultBadge}>Default</span>
                      </Show>
                    </div>
                    <div class={styles.botRtmp}>{truncate(bot.rtmpUrl)}</div>
                    <Show when={status()}>
                      <div
                        class={
                          status() === 'pending'
                            ? styles.testStatusPending
                            : (status() as { ok: boolean }).ok
                              ? styles.testStatusOk
                              : styles.testStatusFail
                        }
                      >
                        {status() === 'pending'
                          ? 'Testing...'
                          : (status() as { ok: boolean; msg: string }).msg}
                      </div>
                    </Show>
                  </div>
                  <div class={styles.botActions}>
                    <button
                      type="button"
                      onClick={() => handleTest(bot)}
                      class={styles.actionButton}
                      data-testid={`streambot-test-${bot.id}`}
                    >
                      Test
                    </button>
                    <button
                      type="button"
                      onClick={() => openEdit(bot)}
                      class={styles.actionButton}
                      data-testid={`streambot-edit-${bot.id}`}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      onClick={() => handleDelete(bot)}
                      class={styles.deleteButton}
                      data-testid={`streambot-delete-${bot.id}`}
                    >
                      Delete
                    </button>
                  </div>
                </div>
              );
            }}
          </For>
        </Show>
      </div>
    </div>
  );
}
