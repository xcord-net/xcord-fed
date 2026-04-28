import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import TriggerConfigEditor from './TriggerConfigEditor';
import { defaultTriggerConfig } from './helpers';

describe('TriggerConfigEditor', () => {
  it('renders without crashing for the Keyword trigger type', () => {
    const { getByLabelText } = render(() => (
      <TriggerConfigEditor
        triggerType="Keyword"
        triggerConfig={defaultTriggerConfig('Keyword')}
        onUpdate={vi.fn()}
      />
    ));
    expect(getByLabelText('Keywords')).toBeInTheDocument();
  });

  it('shows Keyword fields with values from the parsed config', () => {
    const cfg = JSON.stringify({ keywords: ['foo', 'bar'], matchWholeWord: true });
    const { getByLabelText } = render(() => (
      <TriggerConfigEditor triggerType="Keyword" triggerConfig={cfg} onUpdate={vi.fn()} />
    ));
    const keywordsInput = getByLabelText('Keywords') as HTMLInputElement;
    expect(keywordsInput.value).toBe('foo, bar');
    const checkbox = getByLabelText('Match whole word only') as HTMLInputElement;
    expect(checkbox.checked).toBe(true);
  });

  it('invokes onUpdate with a JSON-encoded keywords array when typed', () => {
    const onUpdate = vi.fn();
    const { getByLabelText } = render(() => (
      <TriggerConfigEditor
        triggerType="Keyword"
        triggerConfig={defaultTriggerConfig('Keyword')}
        onUpdate={onUpdate}
      />
    ));
    fireEvent.input(getByLabelText('Keywords'), { target: { value: 'spam, scam' } });
    expect(onUpdate).toHaveBeenCalledTimes(1);
    const arg = onUpdate.mock.calls[0][0] as string;
    const parsed = JSON.parse(arg);
    expect(parsed.keywords).toEqual(['spam', 'scam']);
  });

  it('renders the Regex pattern field for the Regex trigger type', () => {
    const cfg = JSON.stringify({ pattern: 'abc', caseSensitive: false });
    const { getByLabelText } = render(() => (
      <TriggerConfigEditor triggerType="Regex" triggerConfig={cfg} onUpdate={vi.fn()} />
    ));
    const input = getByLabelText('Regex pattern') as HTMLInputElement;
    expect(input.value).toBe('abc');
  });

  it('renders MentionSpam max mentions field', () => {
    const cfg = JSON.stringify({ maxMentions: 7 });
    const { getByLabelText } = render(() => (
      <TriggerConfigEditor triggerType="MentionSpam" triggerConfig={cfg} onUpdate={vi.fn()} />
    ));
    const input = getByLabelText('Max mentions') as HTMLInputElement;
    expect(input.value).toBe('7');
  });

  it('renders LinkFilter blocked and allowed domain fields', () => {
    const cfg = JSON.stringify({
      blockedDomains: ['spam.com'],
      allowedDomains: ['example.com'],
    });
    const { getByLabelText } = render(() => (
      <TriggerConfigEditor triggerType="LinkFilter" triggerConfig={cfg} onUpdate={vi.fn()} />
    ));
    expect((getByLabelText('Blocked domains') as HTMLInputElement).value).toBe('spam.com');
    expect((getByLabelText('Allowed domains') as HTMLInputElement).value).toBe('example.com');
  });
});
