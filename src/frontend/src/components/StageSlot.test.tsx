import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import StageSlot from './StageSlot';

describe('StageSlot', () => {
  function makeProps(overrides: Partial<Parameters<typeof StageSlot>[0]> = {}) {
    return {
      slotIndex: 0,
      userId: null,
      onAssign: vi.fn(),
      onRemove: vi.fn(),
      ...overrides,
    };
  }

  it('renders empty placeholder with slot label', () => {
    const { getByTestId } = render(() => <StageSlot {...makeProps()} />);
    const btn = getByTestId('stage-slot-assign-0');
    expect(btn).toBeInTheDocument();
    expect(btn).toHaveTextContent('Slot 1');
  });

  it('switches to input form when placeholder clicked', () => {
    const { getByTestId } = render(() => <StageSlot {...makeProps({ slotIndex: 2 })} />);
    fireEvent.click(getByTestId('stage-slot-assign-2'));
    expect(getByTestId('stage-slot-input-2')).toBeInTheDocument();
  });

  it('calls onAssign with trimmed userId on confirm', () => {
    const onAssign = vi.fn();
    const { getByTestId } = render(() => <StageSlot {...makeProps({ onAssign })} />);
    fireEvent.click(getByTestId('stage-slot-assign-0'));
    fireEvent.input(getByTestId('stage-slot-input-0'), { target: { value: '  user-123  ' } });
    fireEvent.click(getByTestId('stage-slot-confirm-0'));
    expect(onAssign).toHaveBeenCalledWith('user-123');
  });

  it('does not assign when input is empty', () => {
    const onAssign = vi.fn();
    const { getByTestId } = render(() => <StageSlot {...makeProps({ onAssign })} />);
    fireEvent.click(getByTestId('stage-slot-assign-0'));
    fireEvent.click(getByTestId('stage-slot-confirm-0'));
    expect(onAssign).not.toHaveBeenCalled();
  });

  it('renders displayName and remove button when filled', () => {
    const onRemove = vi.fn();
    const { getByTestId, getByText } = render(() => (
      <StageSlot {...makeProps({ userId: 'u-1', displayName: 'Alice', onRemove })} />
    ));
    expect(getByText('Alice')).toBeInTheDocument();
    fireEvent.click(getByTestId('stage-slot-remove-0'));
    expect(onRemove).toHaveBeenCalledOnce();
  });

  it('renders avatar initial fallback when no avatarUrl', () => {
    const { container } = render(() => (
      <StageSlot {...makeProps({ userId: 'u-1', displayName: 'Bob' })} />
    ));
    expect(container.textContent).toContain('B');
  });
});
