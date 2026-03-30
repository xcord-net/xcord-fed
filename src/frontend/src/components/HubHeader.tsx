import { onMount, onCleanup, createSignal } from 'solid-js';
import { api } from '../api/client';

interface HubHeaderProps {
  hubUrl: string;
  instanceUrl: string;
}

export default function HubHeader(props: HubHeaderProps) {
  const [hubKey, setHubKey] = createSignal<string | null>(null);

  onMount(async () => {
    try {
      const resp = await api.get<{ hubKey: string | null }>('/api/v1/users/@me/hub-key');
      setHubKey(resp.hubKey);
    } catch {
      // No hubKey yet
    }
  });

  const handleMessage = (event: MessageEvent) => {
    if (event.data?.type === 'xcord_hub_key' && event.data.hubKey) {
      const key = event.data.hubKey;
      setHubKey(key);
      api.put('/api/v1/users/@me/hub-key', { hubKey: key }).catch(() => {});
    }
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
