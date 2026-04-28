import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import AttachmentList from './AttachmentList';
import type { MessageAttachment } from '../../types/message';

function makeAttachment(overrides: Partial<MessageAttachment> = {}): MessageAttachment {
  return {
    id: 'a-1',
    fileName: 'document.pdf',
    contentType: 'application/pdf',
    fileSize: 1024,
    downloadUrl: 'https://cdn.xcord-dev.net/a-1',
    ...overrides,
  };
}

describe('AttachmentList', () => {
  it('renders without crashing given an empty list', () => {
    const { container } = render(() => <AttachmentList attachments={[]} />);
    expect(container.querySelector('div')).toBeInTheDocument();
    expect(container.querySelectorAll('a').length).toBe(0);
  });

  it('renders a download link for non-image attachments', () => {
    const attachments = [makeAttachment({ fileName: 'spec.pdf' })];
    const { getByTestId } = render(() => <AttachmentList attachments={attachments} />);
    const link = getByTestId('message-attachment-link') as HTMLAnchorElement;
    expect(link).toBeInTheDocument();
    expect(link.textContent).toBe('spec.pdf');
    expect(link.getAttribute('href')).toBe('https://cdn.xcord-dev.net/a-1');
  });

  it('renders an image thumbnail link when thumbnailUrl is present', () => {
    const attachments = [
      makeAttachment({
        fileName: 'cat.png',
        thumbnailUrl: 'https://cdn.xcord-dev.net/a-1/thumb',
      }),
    ];
    const { getByTestId, queryByTestId } = render(() => (
      <AttachmentList attachments={attachments} />
    ));
    expect(getByTestId('message-attachment-image')).toBeInTheDocument();
    expect(queryByTestId('message-attachment-link')).toBeNull();
    const img = getByTestId('message-attachment-image').querySelector('img') as HTMLImageElement;
    expect(img.getAttribute('src')).toBe('https://cdn.xcord-dev.net/a-1/thumb');
    expect(img.getAttribute('alt')).toBe('cat.png');
  });

  it('uses safe link attributes (target=_blank, rel=noopener)', () => {
    const attachments = [makeAttachment()];
    const { getByTestId } = render(() => <AttachmentList attachments={attachments} />);
    const link = getByTestId('message-attachment-link');
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('renders multiple mixed attachments', () => {
    const attachments = [
      makeAttachment({ id: 'a-1', fileName: 'one.pdf' }),
      makeAttachment({
        id: 'a-2',
        fileName: 'two.png',
        thumbnailUrl: 'https://cdn.xcord-dev.net/a-2/thumb',
      }),
    ];
    const { getAllByTestId } = render(() => <AttachmentList attachments={attachments} />);
    expect(getAllByTestId('message-attachment-link')).toHaveLength(1);
    expect(getAllByTestId('message-attachment-image')).toHaveLength(1);
  });
});
