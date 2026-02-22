import { createSignal, createEffect, onCleanup, Show } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

export interface VoiceRecorderProps {
  conversationId: string;
  onSend: (attachmentId: string) => void;
  onCancel?: () => void;
}

export type RecordingState = 'idle' | 'recording' | 'preview';

interface UploadInitResponse {
  attachmentId: string;
  uploadUrl: string;
}

// ---- Helpers ----

export function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
}

export function buildWaveformBars(analyserNode: AnalyserNode | null, barCount: number): number[] {
  if (!analyserNode) {
    return Array.from({ length: barCount }, () => 0);
  }
  const dataArray = new Uint8Array(analyserNode.frequencyBinCount);
  analyserNode.getByteFrequencyData(dataArray);
  const step = Math.floor(dataArray.length / barCount);
  return Array.from({ length: barCount }, (_, i) => {
    const slice = dataArray.slice(i * step, (i + 1) * step);
    const avg = slice.reduce((a, b) => a + b, 0) / (slice.length || 1);
    return Math.round((avg / 255) * 100);
  });
}

// ---- Component ----

export default function VoiceRecorder(props: VoiceRecorderProps) {
  const [recordingState, setRecordingState] = createSignal<RecordingState>('idle');
  const [durationSeconds, setDurationSeconds] = createSignal(0);
  const [waveformBars, setWaveformBars] = createSignal<number[]>(Array(20).fill(0));
  const [previewUrl, setPreviewUrl] = createSignal<string | null>(null);
  const [isUploading, setIsUploading] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);

  let mediaRecorder: MediaRecorder | null = null;
  let audioChunks: Blob[] = [];
  let recordedBlob: Blob | null = null;
  let timerInterval: ReturnType<typeof setInterval> | undefined;
  let animationFrameId: number | undefined;
  let audioContext: AudioContext | null = null;
  let analyserNode: AnalyserNode | null = null;

  const stopTimer = () => {
    if (timerInterval !== undefined) {
      clearInterval(timerInterval);
      timerInterval = undefined;
    }
  };

  const stopWaveformAnimation = () => {
    if (animationFrameId !== undefined) {
      cancelAnimationFrame(animationFrameId);
      animationFrameId = undefined;
    }
  };

  const startWaveformAnimation = () => {
    const animate = () => {
      setWaveformBars(buildWaveformBars(analyserNode, 20));
      animationFrameId = requestAnimationFrame(animate);
    };
    animationFrameId = requestAnimationFrame(animate);
  };

  const startRecording = async () => {
    setError(null);
    setDurationSeconds(0);
    audioChunks = [];

    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

      // Set up analyser for waveform visualization
      audioContext = new AudioContext();
      analyserNode = audioContext.createAnalyser();
      analyserNode.fftSize = 256;
      const source = audioContext.createMediaStreamSource(stream);
      source.connect(analyserNode);

      mediaRecorder = new MediaRecorder(stream);
      mediaRecorder.ondataavailable = (e: BlobEvent) => {
        if (e.data.size > 0) {
          audioChunks.push(e.data);
        }
      };

      mediaRecorder.onstop = () => {
        recordedBlob = new Blob(audioChunks, { type: 'audio/webm' });
        const url = URL.createObjectURL(recordedBlob);
        setPreviewUrl(url);
        setRecordingState('preview');
        stopWaveformAnimation();
        // Close audio context
        audioContext?.close();
        audioContext = null;
        analyserNode = null;
        // Stop all stream tracks
        stream.getTracks().forEach((t) => t.stop());
      };

      mediaRecorder.start(100);
      setRecordingState('recording');

      // Timer
      timerInterval = setInterval(() => {
        setDurationSeconds((prev) => prev + 1);
      }, 1000);

      startWaveformAnimation();
    } catch (err) {
      setError('Microphone access denied or not available.');
      console.error('Failed to start recording:', err);
    }
  };

  const stopRecording = () => {
    stopTimer();
    if (mediaRecorder && mediaRecorder.state !== 'inactive') {
      mediaRecorder.stop();
    }
  };

  const cancelRecording = () => {
    stopTimer();
    stopWaveformAnimation();
    if (mediaRecorder && mediaRecorder.state !== 'inactive') {
      mediaRecorder.stop();
    }
    // Revoke preview URL if any
    const url = previewUrl();
    if (url) {
      URL.revokeObjectURL(url);
      setPreviewUrl(null);
    }
    recordedBlob = null;
    audioChunks = [];
    setDurationSeconds(0);
    setWaveformBars(Array(20).fill(0));
    setRecordingState('idle');
    props.onCancel?.();
  };

  const sendRecording = async () => {
    if (!recordedBlob) return;
    setIsUploading(true);
    setError(null);

    try {
      const fileName = `voice-message-${Date.now()}.webm`;
      const fileSize = recordedBlob.size;
      const contentType = recordedBlob.type || 'audio/webm';

      // Step 1: Request presigned URL
      const { attachmentId, uploadUrl } = await api.post<UploadInitResponse>('/api/v1/uploads', {
        fileName,
        contentType,
        fileSize,
      });

      // Step 2: PUT blob to presigned URL
      await fetch(uploadUrl, {
        method: 'PUT',
        headers: { 'Content-Type': contentType },
        body: recordedBlob,
      });

      // Step 3: Confirm upload
      await api.post(`/api/v1/attachments/${attachmentId}/confirm`, {});

      // Revoke preview URL
      const url = previewUrl();
      if (url) {
        URL.revokeObjectURL(url);
        setPreviewUrl(null);
      }

      recordedBlob = null;
      audioChunks = [];
      setDurationSeconds(0);
      setWaveformBars(Array(20).fill(0));
      setRecordingState('idle');

      props.onSend(attachmentId);
    } catch (err) {
      setError('Failed to upload voice message. Please try again.');
      console.error('Failed to upload voice recording:', err);
    } finally {
      setIsUploading(false);
    }
  };

  onCleanup(() => {
    stopTimer();
    stopWaveformAnimation();
    const url = previewUrl();
    if (url) URL.revokeObjectURL(url);
    audioContext?.close();
  });

  return (
    <div class="flex flex-col gap-2 px-4 py-2 bg-xcord-bg-primary rounded-lg" aria-label="Voice recorder">
      <Show when={error()}>
        <p class="text-red-400 text-xs">{error()}</p>
      </Show>

      {/* Idle state: record button */}
      <Show when={recordingState() === 'idle'}>
        <button
          class="flex items-center gap-2 px-4 py-2 bg-xcord-brand text-white rounded-lg hover:bg-xcord-brand/80 transition-colors text-sm font-medium"
          onClick={startRecording}
          aria-label="Start voice recording"
        >
          <span class="w-3 h-3 rounded-full bg-white" aria-hidden="true" />
          Record Voice Message
        </button>
      </Show>

      {/* Recording state: waveform + timer + stop */}
      <Show when={recordingState() === 'recording'}>
        <div class="flex flex-col gap-2">
          {/* Waveform visualization */}
          <div
            class="flex items-end gap-0.5 h-10 px-2"
            aria-label="Recording waveform"
            role="img"
          >
            {waveformBars().map((height) => (
              <div
                class="flex-1 bg-xcord-brand rounded-sm transition-all duration-75"
                style={{ height: `${Math.max(4, height)}%` }}
              />
            ))}
          </div>

          <div class="flex items-center justify-between">
            {/* Duration timer */}
            <span
              class="text-xcord-text-primary text-sm font-mono"
              aria-label={`Recording duration ${formatDuration(durationSeconds())}`}
              aria-live="polite"
            >
              {formatDuration(durationSeconds())}
            </span>

            <div class="flex gap-2">
              <button
                class="px-3 py-1.5 bg-xcord-bg-secondary text-xcord-text-muted rounded hover:bg-xcord-bg-secondary/80 transition-colors text-sm"
                onClick={cancelRecording}
                aria-label="Cancel recording"
              >
                Cancel
              </button>
              <button
                class="px-3 py-1.5 bg-red-600 text-white rounded hover:bg-red-700 transition-colors text-sm font-medium"
                onClick={stopRecording}
                aria-label="Stop recording"
              >
                Stop
              </button>
            </div>
          </div>
        </div>
      </Show>

      {/* Preview state: audio player + send/cancel */}
      <Show when={recordingState() === 'preview'}>
        <div class="flex flex-col gap-2">
          <Show when={previewUrl()}>
            {(url) => (
              <audio
                controls
                src={url()}
                class="w-full h-8"
                aria-label="Voice message preview"
              />
            )}
          </Show>

          <div class="flex items-center justify-between">
            <span class="text-xcord-text-muted text-xs">
              Duration: {formatDuration(durationSeconds())}
            </span>

            <div class="flex gap-2">
              <button
                class="px-3 py-1.5 bg-xcord-bg-secondary text-xcord-text-muted rounded hover:bg-xcord-bg-secondary/80 transition-colors text-sm"
                onClick={cancelRecording}
                disabled={isUploading()}
                aria-label="Discard voice message"
              >
                Discard
              </button>
              <button
                class="px-3 py-1.5 bg-xcord-brand text-white rounded hover:bg-xcord-brand/80 transition-colors text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                onClick={sendRecording}
                disabled={isUploading()}
                aria-label="Send voice message"
              >
                {isUploading() ? 'Sending...' : 'Send'}
              </button>
            </div>
          </div>
        </div>
      </Show>
    </div>
  );
}

