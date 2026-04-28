import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@solidjs/testing-library';
import CallOverlay from './CallOverlay';
import { useCalls } from '../stores/call.store';
import type { Call } from '../types/call';

function makeCall(over: Partial<Call> = {}): Call {
  return {
    id: 'call-1',
    conversationId: 'c-1',
    callerId: 'u-1',
    callerUsername: 'alice',
    participantIds: [],
    isVideoCall: false,
    status: 'Ringing',
    startedAt: '2025-01-01T00:00:00Z',
    ...over,
  };
}

describe('CallOverlay', () => {
  beforeEach(() => {
    useCalls().reset();
  });

  it('renders nothing when there is no incoming or active call', () => {
    const { container } = render(() => <CallOverlay />);
    expect(container.textContent).toBe('');
  });

  it('renders the incoming call dialog with caller name', () => {
    useCalls().receiveIncomingCall(makeCall({ callerUsername: 'alice' }));
    const { getByText } = render(() => <CallOverlay />);
    expect(getByText('alice')).toBeInTheDocument();
    expect(getByText(/Voice call incoming/i)).toBeInTheDocument();
    expect(getByText('Answer')).toBeInTheDocument();
    expect(getByText('Decline')).toBeInTheDocument();
  });

  it('shows "Video call incoming" when isVideoCall is true', () => {
    useCalls().receiveIncomingCall(makeCall({ isVideoCall: true }));
    const { getByText } = render(() => <CallOverlay />);
    expect(getByText(/Video call incoming/i)).toBeInTheDocument();
  });

  it('uses the first letter of caller (uppercased) as avatar initial', () => {
    useCalls().receiveIncomingCall(makeCall({ callerUsername: 'zara' }));
    const { container } = render(() => <CallOverlay />);
    expect(container.textContent).toContain('Z');
  });

  it('clears incoming call dialog when reset is called', () => {
    useCalls().receiveIncomingCall(makeCall());
    const { container } = render(() => <CallOverlay />);
    expect(container.textContent).toContain('Answer');
    useCalls().reset();
    expect(container.textContent).not.toContain('Answer');
  });
});
