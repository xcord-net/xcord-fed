import { describe, it, expect } from 'vitest';
import { render } from '@solidjs/testing-library';
import UploadProgress from './UploadProgress';

describe('UploadProgress', () => {
  it('renders without crashing with minimal props', () => {
    const { container } = render(() => <UploadProgress progress={0} />);
    expect(container.firstChild).not.toBeNull();
  });

  it('shows the Uploading... label', () => {
    const { getByText } = render(() => <UploadProgress progress={0} />);
    expect(getByText('Uploading...')).toBeInTheDocument();
  });

  it('renders the progress percentage text', () => {
    const { getByText } = render(() => <UploadProgress progress={42} />);
    expect(getByText('42%')).toBeInTheDocument();
  });

  it('reflects the progress prop in the fill width style', () => {
    const { container } = render(() => <UploadProgress progress={75} />);
    const fill = container.querySelector('[style*="width"]') as HTMLElement;
    expect(fill).not.toBeNull();
    expect(fill.getAttribute('style')).toContain('75%');
  });

  it('renders 0% when progress is zero', () => {
    const { getByText, container } = render(() => <UploadProgress progress={0} />);
    expect(getByText('0%')).toBeInTheDocument();
    const fill = container.querySelector('[style*="width"]') as HTMLElement;
    expect(fill.getAttribute('style')).toContain('0%');
  });

  it('renders 100% when progress is complete', () => {
    const { getByText, container } = render(() => <UploadProgress progress={100} />);
    expect(getByText('100%')).toBeInTheDocument();
    const fill = container.querySelector('[style*="width"]') as HTMLElement;
    expect(fill.getAttribute('style')).toContain('100%');
  });
});
