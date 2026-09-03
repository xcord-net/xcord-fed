import { onMount, onCleanup, createSignal } from 'solid-js';
import { api } from '../api/client';
import { parseHubMessage, HubMessageType } from '../protocol/hubProtocol';

interface HubHeaderProps {
  hubUrl: string;
  instanceUrl: string;
}

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

  // Only the hub origin we already trust, and only the frame we put there:
  // another frame on the same origin does not get to set the key.
  const handleMessage = (event: MessageEvent) => {
    const message = parseHubMessage(event, {
      isTrustedOrigin: (origin) => expectedOrigin !== null && origin === expectedOrigin,
      expectedSource: iframeRef?.contentWindow ?? null,
    });
    if (message?.type !== HubMessageType.HubKey) return;
    setHubKey(message.hubKey);
    api.put('/api/v1/users/@me/hub-key', { hubKey: message.hubKey }).catch(() => {});
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
