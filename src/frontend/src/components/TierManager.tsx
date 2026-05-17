import { For, Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import { useTiers } from '../stores/tier.store';
import { useSubscriptions } from '../stores/subscription.store';
import { getErrorMessage } from '../utils/errors';
import type { Tier } from '../types/tier';
import styles from './TierManager.module.css';

interface TierManagerProps {
  serverId: string;
}

interface Group {
  id: string;
  name: string;
  color: string;
}

interface FormState {
  name: string;
  description: string;
  /** Dollars (stored as text to permit "5.00" input; converted to cents on submit). */
  priceDollars: string;
  currency: string;
  groupIds: Set<string>;
  isActive: boolean;
}

const EMPTY_FORM: FormState = {
  name: '',
  description: '',
  priceDollars: '',
  currency: 'usd',
  groupIds: new Set(),
  isActive: true,
};

function formatPrice(cents: number, currency: string): string {
  const dollars = (cents / 100).toFixed(2);
  return `$${dollars} ${currency.toUpperCase()} / mo`;
}

function dollarsToCents(input: string): number | null {
  const trimmed = input.trim();
  if (!trimmed) return null;
  const value = Number.parseFloat(trimmed);
  if (!Number.isFinite(value) || value < 0) return null;
  return Math.round(value * 100);
}

export default function TierManager(props: TierManagerProps) {
  const tierStore = useTiers();
  const subStore = useSubscriptions();

  const [groups, setGroups] = createSignal<Group[]>([]);
  const [error, setError] = createSignal<string | null>(null);
  const [editingTierId, setEditingTierId] = createSignal<string | null>(null);
  const [showCreateForm, setShowCreateForm] = createSignal(false);
  const [form, setForm] = createSignal<FormState>({ ...EMPTY_FORM, groupIds: new Set() });
  const [isSaving, setIsSaving] = createSignal(false);

  const stripeConfigured = () => subStore.billingConfig(props.serverId)?.stripeConfigured ?? true;

  onMount(async () => {
    try {
      await Promise.all([
        tierStore.fetchTiers(props.serverId),
        subStore.fetchBillingConfig(props.serverId).catch(() => {/* non-fatal */}),
      ]);
      // Groups for the multi-select. Best-effort; failure here doesn't block tier CRUD.
      try {
        const result = await api.get<Group[]>(`/api/v1/servers/${props.serverId}/groups`);
        setGroups(result.map((g) => ({ ...g, id: String(g.id) })));
      } catch {
        // Ignore — groups picker just won't be populated.
      }
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to load tiers'));
    }
  });

  const tiers = () => tierStore.tiers(props.serverId);

  const openCreateForm = () => {
    setForm({ ...EMPTY_FORM, groupIds: new Set() });
    setEditingTierId(null);
    setShowCreateForm(true);
    setError(null);
  };

  const openEditForm = (tier: Tier) => {
    setForm({
      name: tier.name,
      description: tier.description ?? '',
      priceDollars: (tier.priceMonthly / 100).toFixed(2),
      currency: tier.currency,
      groupIds: new Set(tier.groupIds),
      isActive: tier.isActive,
    });
    setEditingTierId(tier.id);
    setShowCreateForm(true);
    setError(null);
  };

  const closeForm = () => {
    setShowCreateForm(false);
    setEditingTierId(null);
    setForm({ ...EMPTY_FORM, groupIds: new Set() });
  };

  const toggleGroup = (groupId: string) => {
    const current = new Set(form().groupIds);
    if (current.has(groupId)) current.delete(groupId);
    else current.add(groupId);
    setForm({ ...form(), groupIds: current });
  };

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    const f = form();
    const cents = dollarsToCents(f.priceDollars);
    if (!f.name.trim()) {
      setError('Tier name is required');
      return;
    }
    if (cents == null || cents < 100) {
      setError('Price must be at least $1.00');
      return;
    }
    setIsSaving(true);
    setError(null);
    try {
      const editId = editingTierId();
      if (editId) {
        await tierStore.updateTier(props.serverId, editId, {
          name: f.name.trim(),
          description: f.description.trim() || undefined,
          priceMonthly: cents,
          groupIds: Array.from(f.groupIds),
          isActive: f.isActive,
        });
      } else {
        await tierStore.createTier(props.serverId, {
          name: f.name.trim(),
          description: f.description.trim() || undefined,
          priceMonthly: cents,
          currency: f.currency,
          groupIds: Array.from(f.groupIds),
        });
      }
      closeForm();
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to save tier'));
    } finally {
      setIsSaving(false);
    }
  };

  const handleDelete = async (tierId: string) => {
    setError(null);
    try {
      await tierStore.deleteTier(props.serverId, tierId);
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Failed to delete tier'));
    }
  };

  const groupNameFor = (id: string): string =>
    groups().find((g) => g.id === id)?.name ?? id;

  return (
    <div class={styles.container}>
      <h2 data-testid="tier-manager-heading" class={styles.heading}>Subscription Tiers</h2>

      <Show when={!stripeConfigured()}>
        <div data-testid="tier-stripe-banner" role="status" class={styles.stripeBanner}>
          Member-tier billing requires a Stripe API key in this instance's configuration.
          Member subscribe buttons will be disabled until configured. See instance admin settings.
        </div>
      </Show>

      <Show when={error()}>
        <div data-testid="tier-manager-error" role="alert" class={styles.errorBanner}>{error()}</div>
      </Show>

      <div class={styles.toolbar}>
        <button
          data-testid="tier-create-button"
          type="button"
          class={styles.createButton}
          onClick={openCreateForm}
          disabled={showCreateForm()}
        >
          Create Tier
        </button>
      </div>

      <Show when={showCreateForm()}>
        <form data-testid="tier-form" class={styles.form} onSubmit={handleSubmit}>
          <div class={styles.fieldGroup}>
            <label for="tier-name" class={styles.fieldLabel}>Name</label>
            <input
              id="tier-name"
              data-testid="tier-form-name"
              type="text"
              required
              maxlength="100"
              value={form().name}
              onInput={(e) => setForm({ ...form(), name: e.currentTarget.value })}
              class={styles.textInput}
            />
          </div>

          <div class={styles.fieldGroup}>
            <label for="tier-price" class={styles.fieldLabel}>Monthly Price (USD)</label>
            <input
              id="tier-price"
              data-testid="tier-form-price"
              type="number"
              min="1"
              step="0.01"
              required
              value={form().priceDollars}
              onInput={(e) => setForm({ ...form(), priceDollars: e.currentTarget.value })}
              class={styles.textInput}
            />
            <p class={styles.fieldHint}>Minimum $1.00. Charged monthly.</p>
          </div>

          <div class={styles.fieldGroup}>
            <label for="tier-currency" class={styles.fieldLabel}>Currency</label>
            <input
              id="tier-currency"
              data-testid="tier-form-currency"
              type="text"
              maxlength="3"
              value={form().currency}
              disabled={!!editingTierId()}
              onInput={(e) => setForm({ ...form(), currency: e.currentTarget.value.toLowerCase() })}
              class={styles.textInput}
            />
          </div>

          <div class={styles.fieldGroup}>
            <label for="tier-description" class={styles.fieldLabel}>Description</label>
            <textarea
              id="tier-description"
              data-testid="tier-form-description"
              rows="2"
              maxlength="500"
              value={form().description}
              onInput={(e) => setForm({ ...form(), description: e.currentTarget.value })}
              class={styles.textarea}
            />
          </div>

          <div class={styles.fieldGroup}>
            <label class={styles.fieldLabel}>Granted Groups</label>
            <div data-testid="tier-form-groups" class={styles.groupChecklist}>
              <Show when={groups().length === 0}>
                <p class={styles.fieldHint}>No groups available.</p>
              </Show>
              <For each={groups()}>
                {(group) => (
                  <label class={styles.groupChecklistItem}>
                    <input
                      data-testid={`tier-form-group-${group.id}`}
                      type="checkbox"
                      checked={form().groupIds.has(group.id)}
                      onChange={() => toggleGroup(group.id)}
                    />
                    <span>{group.name}</span>
                  </label>
                )}
              </For>
            </div>
          </div>

          <Show when={editingTierId()}>
            <div class={styles.fieldGroup}>
              <label class={styles.groupChecklistItem}>
                <input
                  data-testid="tier-form-active"
                  type="checkbox"
                  checked={form().isActive}
                  onChange={(e) => setForm({ ...form(), isActive: e.currentTarget.checked })}
                />
                <span>Active (visible to members)</span>
              </label>
            </div>
          </Show>

          <div class={styles.formActions}>
            <button
              data-testid="tier-form-cancel"
              type="button"
              class={styles.cancelButton}
              onClick={closeForm}
            >
              Cancel
            </button>
            <button
              data-testid="tier-form-save"
              type="submit"
              class={styles.saveButton}
              disabled={isSaving()}
            >
              {isSaving() ? 'Saving...' : (editingTierId() ? 'Save Changes' : 'Create Tier')}
            </button>
          </div>
        </form>
      </Show>

      <Show when={tiers().length === 0 && !tierStore.isLoading(props.serverId)}>
        <p data-testid="tier-empty-state" class={styles.emptyState}>No subscription tiers yet.</p>
      </Show>

      <div class={styles.list}>
        <For each={tiers()}>
          {(tier) => (
            <div data-testid={`tier-item-${tier.id}`} class={styles.tierCard}>
              <div class={styles.tierInfo}>
                <span class={styles.tierName}>{tier.name}</span>
                <span class={styles.tierPrice}>{formatPrice(tier.priceMonthly, tier.currency)}</span>
                <Show when={tier.groupIds.length > 0}>
                  <span class={styles.tierGroups}>
                    Grants: {tier.groupIds.map(groupNameFor).join(', ')}
                  </span>
                </Show>
                <span
                  class={`${styles.tierStatus} ${tier.isActive ? styles.tierStatusActive : styles.tierStatusInactive}`}
                >
                  {tier.isActive ? 'Active' : 'Inactive'}
                </span>
              </div>
              <div class={styles.tierActions}>
                <button
                  data-testid={`tier-edit-${tier.id}`}
                  type="button"
                  class={styles.editButton}
                  onClick={() => openEditForm(tier)}
                >
                  Edit
                </button>
                <button
                  data-testid={`tier-delete-${tier.id}`}
                  type="button"
                  class={styles.deleteButton}
                  onClick={() => handleDelete(tier.id)}
                >
                  Delete
                </button>
              </div>
            </div>
          )}
        </For>
      </div>
    </div>
  );
}
