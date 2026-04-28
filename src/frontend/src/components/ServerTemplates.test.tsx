import { describe, it, expect } from 'vitest';
import { render, fireEvent, waitFor } from '@solidjs/testing-library';
import ServerTemplates, {
  validateTemplateName,
  templateChannelCount,
  templateGroupCount,
} from './ServerTemplates';
import { mockFetch } from '../tests/helpers/mockFetch';

const sampleTemplate = {
  id: 't-1',
  name: 'Gaming Server Template',
  description: 'Pre-built channels for gaming communities',
  channels: [{ name: 'general', type: 'Text', position: 0 }],
  groups: [{ name: 'Mods', roles: [] }],
  usageCount: 12,
  createdAt: '2025-01-01T00:00:00Z',
};

describe('ServerTemplates pure helpers', () => {
  it('validateTemplateName rejects empty names', () => {
    expect(validateTemplateName('')).toMatch(/required/i);
    expect(validateTemplateName('   ')).toMatch(/required/i);
  });

  it('validateTemplateName accepts valid names', () => {
    expect(validateTemplateName('My Template')).toBeNull();
  });

  it('validateTemplateName rejects names over 100 chars', () => {
    expect(validateTemplateName('a'.repeat(101))).toMatch(/100/);
  });

  it('templateChannelCount and templateGroupCount return list lengths', () => {
    expect(templateChannelCount(sampleTemplate as never)).toBe(1);
    expect(templateGroupCount(sampleTemplate as never)).toBe(1);
  });
});

describe('ServerTemplates', () => {
  it('renders the Server Templates header', async () => {
    mockFetch({
      'GET /api/v1/server-templates': () => ({ status: 200, body: [] }),
    });
    const { findByText } = render(() => <ServerTemplates serverId="s-1" />);
    expect(await findByText('Server Templates')).toBeInTheDocument();
  });

  it('shows empty state when no templates exist', async () => {
    mockFetch({
      'GET /api/v1/server-templates': () => ({ status: 200, body: [] }),
    });
    const { findByText } = render(() => <ServerTemplates serverId="s-1" />);
    expect(await findByText('No templates available')).toBeInTheDocument();
  });

  it('renders a template item with name and Use Template action', async () => {
    mockFetch({
      'GET /api/v1/server-templates': () => ({ status: 200, body: [sampleTemplate] }),
    });
    const { findByText } = render(() => <ServerTemplates serverId="s-1" />);
    expect(await findByText('Gaming Server Template')).toBeInTheDocument();
    expect(await findByText('Use Template')).toBeInTheDocument();
  });

  it('shows Save as Template button when isOwner is true', async () => {
    mockFetch({
      'GET /api/v1/server-templates': () => ({ status: 200, body: [] }),
    });
    const { findByText } = render(() => <ServerTemplates serverId="s-1" isOwner={true} />);
    expect(await findByText('Save as Template')).toBeInTheDocument();
  });

  it('opens the create-from-template form when Use Template is clicked', async () => {
    mockFetch({
      'GET /api/v1/server-templates': () => ({ status: 200, body: [sampleTemplate] }),
    });
    const { findByText } = render(() => <ServerTemplates serverId="s-1" />);
    fireEvent.click(await findByText('Use Template'));
    expect(await findByText(/Create Server from/)).toBeInTheDocument();
    expect(await findByText('Create Server')).toBeInTheDocument();
  });

  it('opens the save-template form when Save as Template is clicked', async () => {
    mockFetch({
      'GET /api/v1/server-templates': () => ({ status: 200, body: [] }),
    });
    const { findByText, findAllByText } = render(() => (
      <ServerTemplates serverId="s-1" isOwner={true} />
    ));
    fireEvent.click(await findByText('Save as Template'));
    expect(await findByText('Save Server as Template')).toBeInTheDocument();
    // 'Save Template' appears as the submit-button label
    const matches = await findAllByText('Save Template');
    expect(matches.length).toBeGreaterThan(0);
    await waitFor(() => expect(true).toBe(true));
  });
});
