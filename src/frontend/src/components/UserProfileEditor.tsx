import { Show, onMount, createSignal } from 'solid-js';
import { useProfiles } from '../stores/profile.store';
import PasswordChangeForm from './PasswordChangeForm';
import TwoFactorSetup from './TwoFactorSetup';
import AccountDeletion from './AccountDeletion';
import styles from './UserProfileEditor.module.css';

interface UserProfileEditorProps {
  serverId?: string;
}

export default function UserProfileEditor(props: UserProfileEditorProps) {
  const profileStore = useProfiles();
  const [editMode, setEditMode] = createSignal(false);
  const [displayName, setDisplayName] = createSignal('');
  const [bio, setBio] = createSignal('');
  const [pronouns, setPronouns] = createSignal('');
  const [nickname, setNickname] = createSignal('');
  const [saveSuccess, setSaveSuccess] = createSignal(false);

  onMount(() => {
    profileStore.loadUserProfile();
    if (props.serverId) {
      profileStore.loadServerProfile(props.serverId);
    }
  });

  const handleSaveUserProfile = async () => {
    await profileStore.updateUserProfile({
      displayName: displayName(),
      bio: bio(),
      pronouns: pronouns(),
    });
    setEditMode(false);
    setSaveSuccess(true);
    setTimeout(() => setSaveSuccess(false), 3000);
  };

  const handleSaveServerProfile = async () => {
    if (!props.serverId) return;
    await profileStore.updateServerProfile(props.serverId, {
      nickname: nickname(),
    });
    setEditMode(false);
    setSaveSuccess(true);
    setTimeout(() => setSaveSuccess(false), 3000);
  };

  return (
    <div class={styles.container}>
      <div class={styles.header}>
        <h2 class={styles.heading}>
          {props.serverId ? 'Server Profile' : 'User Profile'}
        </h2>
        <button
          data-testid="profile-edit-button"
          class={styles.editToggleButton}
          onClick={() => setEditMode(!editMode())}
        >
          {editMode() ? 'Cancel' : 'Edit'}
        </button>
      </div>

      <div class={styles.scrollBody}>
        <Show when={profileStore.isLoading}>
          <div class={styles.loadingState}>
            <p class={styles.loadingText}>Loading...</p>
          </div>
        </Show>

        <Show when={!props.serverId && profileStore.userProfile}>
          <div class={styles.profileSection}>
            <div class={styles.banner}>
              <Show when={profileStore.userProfile!.bannerUrl}>
                <img
                  src={profileStore.userProfile!.bannerUrl}
                  alt="Banner"
                  class={styles.bannerImage}
                />
              </Show>
            </div>

            <div class={styles.avatarRow}>
              <div class={styles.avatar}>
                <Show when={profileStore.userProfile!.avatarUrl} fallback={profileStore.userProfile!.username.charAt(0).toUpperCase()}>
                  <img
                    src={profileStore.userProfile!.avatarUrl}
                    alt={profileStore.userProfile!.username}
                    class={styles.avatarImage}
                  />
                </Show>
              </div>
            </div>

            <div class={styles.infoBlockSpaced}>
              <Show when={!editMode()}>
                <div>
                  <h3 data-testid="profile-display-name" class={styles.displayName}>{profileStore.userProfile!.displayName}</h3>
                  <p class={styles.username}>@{profileStore.userProfile!.username}</p>
                </div>

                <Show when={profileStore.userProfile!.pronouns}>
                  <div class={styles.fieldView}>
                    <label class={styles.fieldMeta}>Pronouns</label>
                    <p class={styles.fieldValue}>{profileStore.userProfile!.pronouns}</p>
                  </div>
                </Show>

                <Show when={profileStore.userProfile!.bio}>
                  <div class={styles.fieldView}>
                    <label class={styles.fieldMeta}>Bio</label>
                    <p class={styles.fieldValuePreWrap}>{profileStore.userProfile!.bio}</p>
                  </div>
                </Show>

                <div class={styles.fieldView}>
                  <label class={styles.fieldMeta}>Member Since</label>
                  <p class={styles.fieldValue}>{new Date(profileStore.userProfile!.createdAt).toLocaleDateString()}</p>
                </div>
              </Show>

              <Show when={editMode()}>
                <div class={styles.editForm}>
                  <div class={styles.editFieldGroup}>
                    <label class={styles.editLabel}>Display Name</label>
                    <input
                      data-testid="profile-display-name-input"
                      type="text"
                      class={styles.textInput}
                      value={displayName() || profileStore.userProfile!.displayName}
                      onInput={(e) => setDisplayName(e.currentTarget.value)}
                    />
                  </div>

                  <div class={styles.editFieldGroup}>
                    <label class={styles.editLabel}>Pronouns</label>
                    <input
                      type="text"
                      class={styles.textInput}
                      value={pronouns() || profileStore.userProfile!.pronouns || ''}
                      onInput={(e) => setPronouns(e.currentTarget.value)}
                      placeholder="e.g., they/them"
                    />
                  </div>

                  <div class={styles.editFieldGroup}>
                    <label class={styles.editLabel}>Bio</label>
                    <textarea
                      class={styles.textarea}
                      rows={4}
                      value={bio() || profileStore.userProfile!.bio || ''}
                      onInput={(e) => setBio(e.currentTarget.value)}
                      placeholder="Tell us about yourself..."
                    />
                  </div>

                  <button
                    data-testid="profile-save-button"
                    class={styles.saveButton}
                    onClick={handleSaveUserProfile}
                  >
                    Save Changes
                  </button>
                </div>
              </Show>

              <Show when={saveSuccess()}>
                <p data-testid="profile-save-success" class={styles.saveSuccess}>Profile saved successfully.</p>
              </Show>

              <PasswordChangeForm />
              <TwoFactorSetup twoFactorEnabled={profileStore.userProfile!.twoFactorEnabled ?? false} />
              <AccountDeletion
                scheduledDeletionAt={profileStore.userProfile!.scheduledDeletionAt ?? null}
                onDeletionScheduled={(_date) => profileStore.loadUserProfile()}
                onDeletionCancelled={() => profileStore.loadUserProfile()}
              />
            </div>
          </div>
        </Show>

        <Show when={props.serverId && profileStore.getServerProfile(props.serverId)}>
          <div class={styles.serverProfileSection}>
            <Show when={!editMode()}>
              <div class={styles.fieldView}>
                <label class={styles.fieldMeta}>Server Nickname</label>
                <p class={styles.fieldValue}>
                  {profileStore.getServerProfile(props.serverId!)?.nickname || 'No nickname set'}
                </p>
              </div>
            </Show>

            <Show when={editMode()}>
              <div class={styles.editForm}>
                <div class={styles.editFieldGroup}>
                  <label class={styles.editLabel}>Server Nickname</label>
                  <input
                    type="text"
                    class={styles.textInput}
                    value={nickname() || profileStore.getServerProfile(props.serverId!)?.nickname || ''}
                    onInput={(e) => setNickname(e.currentTarget.value)}
                    placeholder="Enter server nickname..."
                  />
                </div>

                <button
                  class={styles.saveButton}
                  onClick={handleSaveServerProfile}
                >
                  Save Changes
                </button>
              </div>
            </Show>
          </div>
        </Show>
      </div>
    </div>
  );
}
