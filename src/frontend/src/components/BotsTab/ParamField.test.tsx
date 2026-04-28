import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@solidjs/testing-library';
import { ParamField } from './ParamField';
import type { AgentParameterManifest } from './types';

function makeParam(over: Partial<AgentParameterManifest> = {}): AgentParameterManifest {
  return {
    name: 'apiKey',
    type: 'string',
    description: null,
    required: false,
    defaultValue: null,
    ...over,
  };
}

describe('ParamField', () => {
  it('renders without crashing with minimal props', () => {
    const { container } = render(() => (
      <ParamField param={makeParam()} value="" onChange={vi.fn()} />
    ));
    expect(container.textContent).toContain('apiKey');
  });

  it('renders the required asterisk and description when provided', () => {
    const { container } = render(() => (
      <ParamField
        param={makeParam({ required: true, description: 'Used to auth requests' })}
        value=""
        onChange={vi.fn()}
      />
    ));
    expect(container.textContent).toContain('*');
    expect(container.textContent).toContain('Used to auth requests');
  });

  it('renders a string text input that fires onChange when typed', () => {
    const onChange = vi.fn();
    const { container } = render(() => (
      <ParamField param={makeParam()} value="" onChange={onChange} />
    ));
    const input = container.querySelector('input[type="text"]') as HTMLInputElement;
    expect(input).toBeInTheDocument();
    fireEvent.input(input, { target: { value: 'abc' } });
    expect(onChange).toHaveBeenCalledWith('abc');
  });

  it('renders a number input when type is number', () => {
    const onChange = vi.fn();
    const { container } = render(() => (
      <ParamField param={makeParam({ type: 'number' })} value="42" onChange={onChange} />
    ));
    const input = container.querySelector('input[type="number"]') as HTMLInputElement;
    expect(input).toBeInTheDocument();
    expect(input.value).toBe('42');
    fireEvent.input(input, { target: { value: '7' } });
    expect(onChange).toHaveBeenCalledWith('7');
  });

  it('renders a boolean checkbox and emits "true"/"false" on toggle', () => {
    const onChange = vi.fn();
    const { container } = render(() => (
      <ParamField param={makeParam({ type: 'boolean' })} value="false" onChange={onChange} />
    ));
    const checkbox = container.querySelector('input[type="checkbox"]') as HTMLInputElement;
    expect(checkbox).toBeInTheDocument();
    expect(checkbox.checked).toBe(false);
    fireEvent.click(checkbox);
    expect(onChange).toHaveBeenCalledWith('true');
  });

  it('reflects current value in string input', () => {
    const { container } = render(() => (
      <ParamField param={makeParam()} value="hello" onChange={vi.fn()} />
    ));
    const input = container.querySelector('input[type="text"]') as HTMLInputElement;
    expect(input.value).toBe('hello');
  });
});
