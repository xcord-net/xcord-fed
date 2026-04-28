import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import AttachmentPreview from './AttachmentPreview';

describe('AttachmentPreview', () => {
  it('renders without crashing with minimal props', () => {
    const { getByTestId } = render(() => (
      <AttachmentPreview fileName="doc.pdf" fileSize={1024} onRemove={vi.fn()} />
    ));
    expect(getByTestId('compose-attachment-preview')).toBeInTheDocument();
  });

  it('renders the supplied file name', () => {
    const { getByTestId } = render(() => (
      <AttachmentPreview fileName="report.pdf" fileSize={2048} onRemove={vi.fn()} />
    ));
    expect(getByTestId('compose-attachment-filename')).toHaveTextContent('report.pdf');
  });

  it('renders a human-readable file size', () => {
    const { container } = render(() => (
      <AttachmentPreview fileName="image.png" fileSize={1024} onRemove={vi.fn()} />
    ));
    // formatFileSize(1024) renders as "1.0 KB"
    expect(container.textContent).toContain('1.0 KB');
  });

  it('invokes onRemove when remove button is clicked', () => {
    const onRemove = vi.fn();
    const { getByTestId } = render(() => (
      <AttachmentPreview fileName="doc.pdf" fileSize={1024} onRemove={onRemove} />
    ));
    fireEvent.click(getByTestId('compose-attachment-remove'));
    expect(onRemove).toHaveBeenCalledTimes(1);
  });

  it('exposes an accessible label on the remove button', () => {
    const { getByLabelText } = render(() => (
      <AttachmentPreview fileName="doc.pdf" fileSize={1024} onRemove={vi.fn()} />
    ));
    expect(getByLabelText('Remove attachment')).toBeInTheDocument();
  });

  it('handles zero-byte files without crashing', () => {
    const { container } = render(() => (
      <AttachmentPreview fileName="empty.txt" fileSize={0} onRemove={vi.fn()} />
    ));
    expect(container.textContent).toContain('empty.txt');
    expect(container.textContent).toContain('0 B');
  });
});
