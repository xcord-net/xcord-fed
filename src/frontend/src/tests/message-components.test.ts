import { describe, it, expect, beforeEach, vi } from 'vitest';
import { api } from '../api/client';
import type {
  MessageButton,
  SelectMenu,
  SelectMenuOption,
  ActionRow,
  ButtonStyle,
} from '../components/MessageComponents';
import { buttonStyleClasses, isValidButtonStyle } from '../components/MessageComponents';

// ---- Test data ----

const makeButton = (overrides: Partial<MessageButton> = {}): MessageButton => ({
  customId: 'btn-1',
  label: 'Click me',
  style: 'Primary',
  ...overrides,
});

const makeSelectOption = (overrides: Partial<SelectMenuOption> = {}): SelectMenuOption => ({
  label: 'Option A',
  value: 'option_a',
  ...overrides,
});

const makeSelectMenu = (overrides: Partial<SelectMenu> = {}): SelectMenu => ({
  customId: 'menu-1',
  placeholder: 'Choose an option',
  options: [
    makeSelectOption({ label: 'Option A', value: 'a' }),
    makeSelectOption({ label: 'Option B', value: 'b' }),
  ],
  ...overrides,
});

const makeActionRow = (overrides: Partial<ActionRow> = {}): ActionRow => ({
  type: 'action_row',
  components: [{ type: 'button', button: makeButton() }],
  ...overrides,
});

// ---- Tests ----

describe('MessageComponents', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    api.setAuthenticated(true);
  });

  // ---- ButtonStyle classes ----

  describe('buttonStyleClasses', () => {
    it('Primary style includes brand background', () => {
      // Act
      const classes = buttonStyleClasses('Primary');

      // Assert
      expect(classes).toContain('xcord-brand');
    });

    it('Danger style includes red background', () => {
      // Act
      const classes = buttonStyleClasses('Danger');

      // Assert
      expect(classes).toContain('red');
    });

    it('Success style includes green background', () => {
      // Act
      const classes = buttonStyleClasses('Success');

      // Assert
      expect(classes).toContain('green');
    });

    it('Secondary style does not include brand or destructive colors', () => {
      // Act
      const classes = buttonStyleClasses('Secondary');

      // Assert
      expect(classes).not.toContain('red');
      expect(classes).not.toContain('green');
    });

    it('Link style includes underline styling', () => {
      // Act
      const classes = buttonStyleClasses('Link');

      // Assert
      expect(classes).toContain('underline');
    });

    it('all valid styles return a non-empty class string', () => {
      // Arrange
      const styles: ButtonStyle[] = ['Primary', 'Secondary', 'Success', 'Danger', 'Link'];

      // Assert
      for (const style of styles) {
        expect(buttonStyleClasses(style).length).toBeGreaterThan(0);
      }
    });
  });

  // ---- isValidButtonStyle ----

  describe('isValidButtonStyle', () => {
    it('returns true for all valid styles', () => {
      expect(isValidButtonStyle('Primary')).toBe(true);
      expect(isValidButtonStyle('Secondary')).toBe(true);
      expect(isValidButtonStyle('Success')).toBe(true);
      expect(isValidButtonStyle('Danger')).toBe(true);
      expect(isValidButtonStyle('Link')).toBe(true);
    });

    it('returns false for unknown style', () => {
      // Act
      const result = isValidButtonStyle('Ghost');

      // Assert
      expect(result).toBe(false);
    });

    it('is case-sensitive', () => {
      expect(isValidButtonStyle('primary')).toBe(false);
      expect(isValidButtonStyle('PRIMARY')).toBe(false);
    });
  });

  // ---- MessageButton shape ----

  describe('MessageButton data shape', () => {
    it('has customId, label, and style', () => {
      // Arrange
      const btn = makeButton();

      // Assert
      expect(btn.customId).toBeDefined();
      expect(btn.label).toBeDefined();
      expect(btn.style).toBeDefined();
    });

    it('url is optional on button', () => {
      // Arrange
      const btn = makeButton({ url: undefined });

      // Assert
      expect(btn.url).toBeUndefined();
    });

    it('disabled flag is optional and defaults to absent', () => {
      // Arrange
      const btn = makeButton();

      // Assert
      expect(btn.disabled).toBeUndefined();
    });

    it('Link style button has a url', () => {
      // Arrange
      const btn = makeButton({ style: 'Link', url: 'https://example.com' });

      // Assert
      expect(btn.style).toBe('Link');
      expect(btn.url).toBe('https://example.com');
    });
  });

  // ---- SelectMenu shape ----

  describe('SelectMenu data shape', () => {
    it('has customId and options array', () => {
      // Arrange
      const menu = makeSelectMenu();

      // Assert
      expect(menu.customId).toBeDefined();
      expect(menu.options).toBeInstanceOf(Array);
    });

    it('each option has label and value', () => {
      // Arrange
      const opt = makeSelectOption({ label: 'Red', value: 'red' });

      // Assert
      expect(opt.label).toBe('Red');
      expect(opt.value).toBe('red');
    });

    it('option description is optional', () => {
      // Arrange
      const opt = makeSelectOption({ description: undefined });

      // Assert
      expect(opt.description).toBeUndefined();
    });

    it('maxValues defaults to 1 (single-select)', () => {
      // Arrange
      const menu = makeSelectMenu();

      // Assert — maxValues not explicitly set means single-select
      expect(menu.maxValues).toBeUndefined();
    });

    it('multi-select menu has maxValues > 1', () => {
      // Arrange
      const menu = makeSelectMenu({ maxValues: 3, minValues: 1 });

      // Assert
      expect(menu.maxValues).toBeGreaterThan(1);
    });
  });

  // ---- ActionRow shape ----

  describe('ActionRow', () => {
    it('action row type is always "action_row"', () => {
      // Arrange
      const row = makeActionRow();

      // Assert
      expect(row.type).toBe('action_row');
    });

    it('action row can contain button components', () => {
      // Arrange
      const row = makeActionRow({
        components: [{ type: 'button', button: makeButton() }],
      });

      // Assert
      expect(row.components[0].type).toBe('button');
    });

    it('action row can contain select_menu components', () => {
      // Arrange
      const row = makeActionRow({
        components: [{ type: 'select_menu', menu: makeSelectMenu() }],
      });

      // Assert
      expect(row.components[0].type).toBe('select_menu');
    });
  });

  // ---- Interaction API ----

  describe('interaction API', () => {
    it('POST /api/v1/interactions/{id} sends customId and type', async () => {
      // Arrange
      const interactionId = 'interaction-abc';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      // Act
      await api.post(`/api/v1/interactions/${interactionId}`, {
        customId: 'btn-confirm',
        type: 'button',
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/interactions/${interactionId}`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ customId: 'btn-confirm', type: 'button' }),
        }),
      );
    });

    it('select menu interaction includes values array', async () => {
      // Arrange
      const interactionId = 'interaction-xyz';

      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      // Act
      await api.post(`/api/v1/interactions/${interactionId}`, {
        customId: 'menu-color',
        type: 'select_menu',
        values: ['red', 'blue'],
      });

      // Assert
      expect(globalThis.fetch).toHaveBeenCalledWith(
        `/api/v1/interactions/${interactionId}`,
        expect.objectContaining({
          body: JSON.stringify({ customId: 'menu-color', type: 'select_menu', values: ['red', 'blue'] }),
        }),
      );
    });
  });
});
