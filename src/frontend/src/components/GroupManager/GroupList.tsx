import { For, Show } from 'solid-js';
import styles from './GroupList.module.css';
import { DEFAULT_GROUP_COLOR } from '../../constants/colors';

interface Group {
  id: string;
  serverId: string;
  name: string;
  color: string;
  roles: number;
  position: number;
  isHoisted: boolean;
  isMentionable: boolean;
  limitsJson?: string;
}

interface GroupListProps {
  groups: Group[];
  isLoading: boolean;
  selectedGroupId: string | null;
  onSelect: (group: Group) => void;
}

export default function GroupList(props: GroupListProps) {
  return (
    <div data-testid="group-list-sidebar" class={styles.sidebar}>
      <Show when={props.isLoading}>
        <div class={styles.sidebarLoading}>
          <p class={styles.mutedText}>Loading groups...</p>
        </div>
      </Show>

      <Show when={!props.isLoading && props.groups.length === 0}>
        <div class={styles.sidebarEmpty}>
          <p class={styles.mutedText}>No groups yet.</p>
        </div>
      </Show>

      <For each={props.groups}>
        {(group) => (
          <button
            data-testid={group.name === '@everyone' ? 'group-item-everyone' : `group-item-${group.id}`}
            type="button"
            onClick={() => props.onSelect(group)}
            class={`${styles.groupItem} ${props.selectedGroupId === group.id ? styles.groupItemActive : ''}`}
            aria-pressed={props.selectedGroupId === group.id}
          >
            {/* Color dot */}
            <span
              class={styles.groupColorDot}
              style={{ 'background-color': group.color || DEFAULT_GROUP_COLOR }}
              aria-hidden="true"
            />
            <span class={styles.groupName}>{group.name}</span>
          </button>
        )}
      </For>
    </div>
  );
}
