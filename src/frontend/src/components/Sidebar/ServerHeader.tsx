import { Show } from 'solid-js';
import Menu from '../ui/Menu';
import { tooltip } from '../../directives/tooltip';
import { ChevronDownIcon, PlusIcon } from './icons';
import sharedStyles from './Sidebar.module.css';
import Flexbox from '../ui/Flexbox';
import styles from './ServerHeader.module.css';

// Ensure the directive is not tree-shaken
void tooltip;

export interface ServerHeaderProps {
  server: { id: string; name: string; iconUrl?: string };
  selectedServerId: string | null;
  showServerMenu: boolean;
  /** When true, render the Membership menu item. Gated by caller on
   *  `canUseMemberTiers` flag AND the current user NOT being the server owner
   *  (owners manage tiers via TierManager; they don't subscribe to their own server). */
  canShowMembership?: boolean;
  onMenuOpen: () => void;
  onMenuClose: () => void;
  onNavigateToServer: (serverId: string) => void;
  onCreateChannel: () => void;
  onOpenServerSettings: () => void;
  onOpenInvite: () => void;
  onToggleEvents: () => void;
  onOpenGroups: () => void;
  onOpenMembership?: () => void;
  onLeaveServer: () => void;
}

export default function ServerHeader(props: ServerHeaderProps) {
  let menuButtonRef!: HTMLButtonElement;

  const initials = (name: string) =>
    name.length <= 4 ? name : name.split(/\s+/).map(w => w[0]).join('').slice(0, 3).toUpperCase();

  return (
    <Flexbox align="center" gap={0.5} class={styles.serverHeader}>
      <button
        data-testid="nav-server-icon"
        aria-label={props.server.name}
        class={styles.serverIconButton}
        onClick={() => props.onNavigateToServer(props.server.id)}
      >
        <Show when={props.server.iconUrl} fallback={
          <span class={styles.serverIconText}>{initials(props.server.name)}</span>
        }>
          <img src={props.server.iconUrl} alt={props.server.name} class={styles.serverIconImg} />
        </Show>
      </button>

      {/* Create channel - always in the header row, never displaces anything */}
      <button
        data-testid="create-channel-button"
        aria-label="Create Channel"
        title="Create Channel"
        use:tooltip="Create Channel"
        class={styles.headerPlusButton}
        onClick={() => props.onCreateChannel()}
      >
        <PlusIcon class={styles.createChannelPlusIcon} />
      </button>

      {/* Server name + menu - CSS expanded-only */}
      <h2 class={`expanded-only ${styles.serverNameHeading}`} data-testid="server-name-heading">{props.server.name}</h2>
      <Show when={props.selectedServerId}>
        <button
          data-testid="server-menu-trigger"
          ref={menuButtonRef}
          type="button"
          aria-label="Server options"
          aria-haspopup="menu"
          aria-expanded={props.showServerMenu}
          title="Server Options"
          onClick={() => props.onMenuOpen()}
          class={`expanded-only ${styles.serverMenuTrigger}`}
        >
          <ChevronDownIcon class={styles.chevronIcon} />
        </button>
      </Show>

      {/* Server dropdown menu */}
      <Menu
        open={props.showServerMenu}
        onClose={() => { props.onMenuClose(); menuButtonRef?.focus(); }}
        anchorRef={menuButtonRef}
        placement="bottom-start"
      >
        <button
          data-testid="server-menu-settings"
          type="button"
          role="menuitem"
          onClick={() => { props.onOpenServerSettings(); props.onMenuClose(); }}
          class={sharedStyles.menuItem}
        >
          Server Settings
        </button>
        <button
          data-testid="server-menu-invite"
          type="button"
          role="menuitem"
          onClick={() => { props.onOpenInvite(); props.onMenuClose(); }}
          class={sharedStyles.menuItem}
        >
          Invite People
        </button>
        <button
          data-testid="server-menu-events"
          type="button"
          role="menuitem"
          onClick={() => { props.onToggleEvents(); props.onMenuClose(); }}
          class={sharedStyles.menuItem}
        >
          Scheduled Events
        </button>
        <button
          data-testid="server-menu-groups"
          type="button"
          role="menuitem"
          onClick={() => { props.onOpenGroups(); props.onMenuClose(); }}
          class={sharedStyles.menuItem}
        >
          Groups &amp; Permissions
        </button>
        <Show when={props.canShowMembership}>
          <button
            data-testid="server-menu-membership"
            type="button"
            role="menuitem"
            onClick={() => { props.onOpenMembership?.(); props.onMenuClose(); }}
            class={sharedStyles.menuItem}
          >
            Membership
          </button>
        </Show>
        <div class={sharedStyles.menuDivider} />
        <button
          data-testid="server-menu-leave"
          type="button"
          role="menuitem"
          onClick={() => { props.onMenuClose(); props.onLeaveServer(); }}
          class={sharedStyles.menuItemDanger}
        >
          Leave Server
        </button>
      </Menu>
    </Flexbox>
  );
}
