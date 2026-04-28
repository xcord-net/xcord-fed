import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import ReplyPreview from './ReplyPreview';

describe('ReplyPreview', () => {
  it('renders without crashing with minimal props', () => {
    const { container } = render(() => <ReplyPreview onCancel={vi.fn()} />);
    expect(container.firstChild).not.toBeNull();
  });

  it('renders the reply label text', () => {
    const { getByText } = render(() => <ReplyPreview onCancel={vi.fn()} />);
    expect(getByText('Replying to a message')).toBeInTheDocument();
  });

  it('renders a close button', () => {
    const { container } = render(() => <ReplyPreview onCancel={vi.fn()} />);
    const button = container.querySelector('button');
    expect(button).not.toBeNull();
    expect(button).toHaveTextContent('x');
  });

  it('invokes onCancel when the close button is clicked', () => {
    const onCancel = vi.fn();
    const { container } = render(() => <ReplyPreview onCancel={onCancel} />);
    const button = container.querySelector('button')!;
    fireEvent.click(button);
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('does not call onCancel before any interaction', () => {
    const onCancel = vi.fn();
    render(() => <ReplyPreview onCancel={onCancel} />);
    expect(onCancel).not.toHaveBeenCalled();
  });
});
