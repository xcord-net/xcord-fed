import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import Captcha from './Captcha';
import { mockFetch } from '../tests/helpers/mockFetch';

const challenge = {
  captchaId: 'c-1',
  imageUrl: '/api/v1/auth/captcha/c-1.gif',
  audioUrl: '/api/v1/auth/captcha/c-1.wav',
};

describe('Captcha', () => {
  it('renders the captcha image once the challenge loads', async () => {
    mockFetch({
      'GET /api/v1/auth/captcha': () => ({ status: 200, body: challenge }),
    });
    const { findByTestId } = render(() => <Captcha onSolved={() => {}} />);
    const img = await findByTestId('captcha-image') as HTMLImageElement;
    expect(img).toBeInTheDocument();
    expect(img.src).toContain(challenge.imageUrl);
  });

  it('swaps to the audio element when the audio toggle is clicked', async () => {
    mockFetch({
      'GET /api/v1/auth/captcha': () => ({ status: 200, body: challenge }),
    });
    const { findByTestId, getByTestId, queryByTestId } = render(() => <Captcha onSolved={() => {}} />);
    await findByTestId('captcha-image');
    fireEvent.click(getByTestId('captcha-audio-toggle'));
    const audio = await findByTestId('captcha-audio') as HTMLAudioElement;
    expect(audio).toBeInTheDocument();
    expect(audio.src).toContain(challenge.audioUrl);
    expect(queryByTestId('captcha-image')).not.toBeInTheDocument();
  });

  it('reports disabled captcha via onSolved and hides the widget', async () => {
    mockFetch({
      'GET /api/v1/auth/captcha': () => ({ status: 200, body: { captchaId: 'disabled', imageUrl: '', audioUrl: '' } }),
    });
    const onSolved = vi.fn();
    const { queryByTestId } = render(() => <Captcha onSolved={onSolved} />);
    await waitFor(() => expect(onSolved).toHaveBeenCalledWith('disabled', ''));
    expect(queryByTestId('captcha-image')).not.toBeInTheDocument();
    expect(queryByTestId('captcha-input')).not.toBeInTheDocument();
  });

  it('fires onSolved with the captcha id and typed answer', async () => {
    mockFetch({
      'GET /api/v1/auth/captcha': () => ({ status: 200, body: challenge }),
    });
    const onSolved = vi.fn();
    const { findByTestId } = render(() => <Captcha onSolved={onSolved} />);
    const input = await findByTestId('captcha-input') as HTMLInputElement;
    fireEvent.input(input, { target: { value: 'ABCD' } });
    await waitFor(() => expect(onSolved).toHaveBeenCalledWith('c-1', 'ABCD'));
  });

  it('refetches a new challenge when "New" is clicked', async () => {
    let call = 0;
    mockFetch({
      'GET /api/v1/auth/captcha': () => {
        call += 1;
        return {
          status: 200,
          body: { captchaId: `c-${call}`, imageUrl: `/api/v1/auth/captcha/c-${call}.gif`, audioUrl: `/api/v1/auth/captcha/c-${call}.wav` },
        };
      },
    });
    const onSolved = vi.fn();
    const { findByTestId } = render(() => <Captcha onSolved={onSolved} />);
    const img = await findByTestId('captcha-image') as HTMLImageElement;
    expect(img.src).toContain('c-1.gif');

    fireEvent.click(await findByTestId('captcha-new'));
    await waitFor(() => expect(img.src).toContain('c-2.gif'));
    expect(call).toBe(2);
  });
});
