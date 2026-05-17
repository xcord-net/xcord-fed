import { Show } from 'solid-js';
import StatusPicker from '../StatusPicker';
import { tooltip } from '../../directives/tooltip';
import { AdminShieldIcon, GearIcon, LogoutIcon } from './icons';
import Flexbox from '../ui/Flexbox';
import styles from './UserStatusBar.module.css';

// Ensure the directive is not tree-shaken
void tooltip;

export interface UserStatusBarProps {
  profile: { username: string; displayName?: string; avatarUrl?: string } | null | undefined;
  isAdmin: boolean;
  version: string | null;
  onOpenSettings: () => void;
  onLogout: () => void;
}

const isValidVersion = (v: string) =>
  v !== '0.0.0' && v !== '0.0.0-dev' && v !== '';

/** Bottom panel: version badge, avatar with status picker + admin shield,
 *  username, settings + logout buttons. */
export default function UserStatusBar(props: UserStatusBarProps) {
  return (
    <div class={styles.userPanel}>
      {/* Version badge - expanded-only via CSS */}
      <Show when={props.version && isValidVersion(props.version)}>
        <div class={`expanded-only ${styles.versionBadgeWrapper}`}>
          <span data-testid="version-badge" class={styles.versionBadge}>
            v{props.version}
          </span>
        </div>
      </Show>

      <Show when={props.profile}>
        {(profile) => (
          <Flexbox align="center" gap={0.5} data-testid="nav-user-avatar" id="current-user-bar" class={styles.userBar}>
            {/* Avatar */}
            <div class={styles.avatarGroup}>
              <div
                class={styles.avatarButton}
                onClick={() => props.onOpenSettings()}
              >
                <Show when={profile().avatarUrl} fallback={<span class={styles.avatarInitial}>{profile().username.charAt(0).toUpperCase()}</span>}>
                  <img src={profile().avatarUrl} alt={profile().username} class={styles.avatarImg} />
                </Show>
              </div>
              <StatusPicker />
              <Show when={props.isAdmin}>
                <div class={styles.adminBadge} title="Admin">
                  <AdminShieldIcon class={styles.adminIcon} />
                </div>
              </Show>
              {/* Tooltip - shown via CSS when collapsed (not hovered) */}
              <div class={`collapsed-only ${styles.userTooltip}`}>
                {profile().displayName || profile().username}
              </div>
            </div>

            {/* Username - expanded-only via CSS */}
            <span class={`expanded-only ${styles.username}`}>{profile().displayName || profile().username}</span>

            {/* Settings + Logout */}
            <Flexbox align="center" gap={0.25} class={styles.userActions}>
              <button
                data-testid="nav-user-settings-button"
                class={styles.iconButton}
                aria-label="Settings"
                title="Settings"
                use:tooltip="Settings"
                onClick={() => props.onOpenSettings()}
              >
                <GearIcon class={styles.smallIcon} />
              </button>
              <button
                data-testid="nav-logout-button"
                class={`${styles.iconButton} ${styles.iconButtonDanger}`}
                aria-label="Log Out"
                title="Log Out"
                use:tooltip="Log Out"
                onClick={() => props.onLogout()}
              >
                <LogoutIcon class={styles.smallIcon} />
              </button>
            </Flexbox>
          </Flexbox>
        )}
      </Show>
    </div>
  );
}
