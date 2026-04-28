import { describe, it, expect } from 'vitest';
import { render, waitFor } from '@solidjs/testing-library';
import HubHeader from './HubHeader';
import { mockFetch } from '../tests/helpers/mockFetch';

describe('HubHeader', () => {
  it('renders an iframe with the hub header URL', () => {
    mockFetch({ 'GET /api/v1/users/@me/hub-key': () => ({ status: 200, body: { hubKey: null } }) });
    const { container } = render(() => (
      <HubHeader hubUrl="https://hub.test" instanceUrl="https://inst.test" />
    ));
    const iframe = container.querySelector('iframe')!;
    expect(iframe).not.toBeNull();
    expect(iframe.getAttribute('src')).toContain('https://hub.test/api/v1/header');
    expect(iframe.getAttribute('src')).toContain('serverUrl=');
  });

  it('omits hubKey from query when none stored', () => {
    mockFetch({ 'GET /api/v1/users/@me/hub-key': () => ({ status: 200, body: { hubKey: null } }) });
    const { container } = render(() => (
      <HubHeader hubUrl="https://hub.test" instanceUrl="https://inst.test" />
    ));
    expect(container.querySelector('iframe')!.getAttribute('src')).not.toContain('hubKey=');
  });

  it('appends hubKey to iframe URL after fetch resolves', async () => {
    mockFetch({ 'GET /api/v1/users/@me/hub-key': () => ({ status: 200, body: { hubKey: 'abc-123' } }) });
    const { container } = render(() => (
      <HubHeader hubUrl="https://hub.test" instanceUrl="https://inst.test" />
    ));
    await waitFor(() => {
      expect(container.querySelector('iframe')!.getAttribute('src')).toContain('hubKey=abc-123');
    });
  });

  it('encodes the instance URL into serverUrl param', () => {
    mockFetch({ 'GET /api/v1/users/@me/hub-key': () => ({ status: 200, body: { hubKey: null } }) });
    const { container } = render(() => (
      <HubHeader hubUrl="https://hub.test" instanceUrl="https://my-inst.test/path" />
    ));
    const src = container.querySelector('iframe')!.getAttribute('src')!;
    expect(src).toContain('serverUrl=https%3A%2F%2Fmy-inst.test%2Fpath');
  });

  it('survives a hub-key fetch failure without throwing', async () => {
    mockFetch({ 'GET /api/v1/users/@me/hub-key': () => ({ status: 500, body: { error: 'nope' } }) });
    const { container } = render(() => (
      <HubHeader hubUrl="https://hub.test" instanceUrl="https://inst.test" />
    ));
    await waitFor(() => {
      expect(container.querySelector('iframe')).not.toBeNull();
    });
  });
});
