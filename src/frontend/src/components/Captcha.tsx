import { createSignal, onMount, Show } from 'solid-js';
import { api } from '../api/client';
import { getErrorMessage } from '../utils/errors';
import styles from './Captcha.module.css';

interface CaptchaChallengeResponse {
  captchaId: string;
  imageUrl: string;
  audioUrl: string;
}

interface CaptchaProps {
  onSolved: (captchaId: string, answer: string) => void;
  baseUrl?: string;
}

const DISABLED_ID = 'disabled';

export default function Captcha(props: CaptchaProps) {
  const baseUrl = props.baseUrl ?? '/api/v1/auth/captcha';

  const [captchaId, setCaptchaId] = createSignal('');
  const [imageUrl, setImageUrl] = createSignal('');
  const [audioUrl, setAudioUrl] = createSignal('');
  const [answer, setAnswer] = createSignal('');
  const [useAudio, setUseAudio] = createSignal(false);
  const [isDisabled, setIsDisabled] = createSignal(false);
  const [isLoading, setIsLoading] = createSignal(false);
  const [loaded, setLoaded] = createSignal(false);
  const [error, setError] = createSignal('');

  async function loadChallenge() {
    setIsLoading(true);
    setError('');
    try {
      const response = await api.get<CaptchaChallengeResponse>(
        `${baseUrl}?t=${Date.now()}`,
      );
      setAnswer('');
      setUseAudio(false);

      if (response.captchaId === DISABLED_ID) {
        setIsDisabled(true);
        setCaptchaId(DISABLED_ID);
        props.onSolved(DISABLED_ID, '');
        return;
      }

      setIsDisabled(false);
      setCaptchaId(response.captchaId);
      setImageUrl(response.imageUrl);
      setAudioUrl(response.audioUrl);
      setLoaded(true);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load captcha'));
    } finally {
      setIsLoading(false);
    }
  }

  onMount(() => {
    loadChallenge();
  });

  function handleAnswerInput(e: Event) {
    const value = (e.target as HTMLInputElement).value;
    setAnswer(value);
    props.onSolved(captchaId(), value);
  }

  return (
    <Show when={!isDisabled()}>
      <div class={styles.wrapper}>
        <label class={styles.label} for="captcha-answer">Confirm you are human</label>

        <Show when={error()}>
          <p class={styles.errorText}>{error()}</p>
        </Show>

        <Show when={loaded()}>
          <Show when={!useAudio()}>
            <img
              data-testid="captcha-image"
              class={styles.image}
              src={imageUrl()}
              alt="Animated captcha challenge - type the letters formed by the moving dots"
            />
          </Show>

          <Show when={useAudio()}>
            <audio
              data-testid="captcha-audio"
              class={styles.audio}
              controls
              src={audioUrl()}
            />
          </Show>

          <div class={styles.controlsRow}>
            <button
              type="button"
              data-testid="captcha-new"
              class={styles.controlButton}
              onClick={loadChallenge}
              disabled={isLoading()}
            >
              New
            </button>
            <button
              type="button"
              data-testid="captcha-audio-toggle"
              class={styles.controlButton}
              onClick={() => setUseAudio(!useAudio())}
            >
              {useAudio() ? "Can't hear it? Use image" : "Can't see it? Use audio"}
            </button>
          </div>

          <input
            id="captcha-answer"
            data-testid="captcha-input"
            type="text"
            class={styles.input}
            value={answer()}
            onInput={handleAnswerInput}
            autocomplete="off"
            placeholder="Type what you see or hear"
          />
        </Show>
      </div>
    </Show>
  );
}
