import { onMount, onCleanup, createSignal } from 'solid-js';
import { api } from '../api/client';

interface HubHeaderProps {
  hubUrl: string;
  instanceUrl: string;
}

const HUB_KEY_PATTERN = /^[A-Za-z0-9_-]{1,64}$/;

export default function HubHeader(props: HubHeaderProps) {
  const [hubKey, setHubKey] = createSignal<string | null>(null);
  let iframeRef: HTMLIFrameElement | undefined;

  // Pin to the hub origin we already trust at runtime; federation precludes a static allowlist.
  let expectedOrigin: string | null = null;
  try {
    expectedOrigin = new URL(props.hubUrl).origin;
  } catch {
    expectedOrigin = null;
  }

  onMount(async () => {
    try {
      const resp = await api.get<{ hubKey: string | null }>('/api/v1/users/@me/hub-key');
      setHubKey(resp.hubKey);
    } catch {
      // No hubKey yet
    }
  });

  const handleMessage = (event: MessageEvent) => {
    if (expectedOrigin === null) return;
    if (event.origin !== expectedOrigin) return;
    if (event.source !== iframeRef?.contentWindow) return;
    const data = event.data;
    if (!data || data.type !== 'xcord_hub_key') return;
    const key = data.hubKey;
    if (typeof key !== 'string' || !HUB_KEY_PATTERN.test(key)) return;
    setHubKey(key);
    api.put('/api/v1/users/@me/hub-key', { hubKey: key }).catch(() => {});
  };

  onMount(() => window.addEventListener('message', handleMessage));
  onCleanup(() => window.removeEventListener('message', handleMessage));

  const iframeSrc = () => {
    const params = new URLSearchParams();
    params.set('serverUrl', props.instanceUrl);
    const key = hubKey();
    if (key) params.set('hubKey', key);
    return `${props.hubUrl}/api/v1/header?${params}`;
  };

  return (
    <iframe
      ref={iframeRef}
      src={iframeSrc()}
      style={{
        width: '100%',
        height: '48px',
        border: 'none',
        display: 'block',
        'flex-shrink': '0',
      }}
      allow="clipboard-read; clipboard-write"
    />
  );
}
