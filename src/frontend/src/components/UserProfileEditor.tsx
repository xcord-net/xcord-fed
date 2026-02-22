import { Show, onMount, createSignal } from 'solid-js';
import { useProfiles } from '../stores/profile.store';
import PasswordChangeForm from './PasswordChangeForm';
import TwoFactorSetup from './TwoFactorSetup';
import AccountDeletion from './AccountDeletion';

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
  };

  const handleSaveServerProfile = async () => {
    if (!props.serverId) return;
    await profileStore.updateServerProfile(props.serverId, {
      nickname: nickname(),
    });
    setEditMode(false);
  };

  return (
    <div class="flex flex-col h-full bg-xcord-bg-secondary">
      <div class="px-4 py-3 border-b border-xcord-border flex items-center justify-between">
        <h2 class="text-white font-semibold">
          {props.serverId ? 'Server Profile' : 'User Profile'}
        </h2>
        <button
          class="text-xcord-brand hover:underline text-sm"
          onClick={() => setEditMode(!editMode())}
        >
          {editMode() ? 'Cancel' : 'Edit'}
        </button>
      </div>

      <div class="flex-1 overflow-y-auto p-4">
        <Show when={profileStore.isLoading}>
          <div class="flex items-center justify-center h-32">
            <p class="text-xcord-text-muted">Loading...</p>
          </div>
        </Show>

        <Show when={!props.serverId && profileStore.userProfile}>
          <div class="space-y-4">
            <div class="relative h-32 bg-gradient-to-r from-xcord-brand to-purple-600 rounded-t-lg">
              <Show when={profileStore.userProfile!.bannerUrl}>
                <img
                  src={profileStore.userProfile!.bannerUrl}
                  alt="Banner"
                  class="w-full h-full object-cover rounded-t-lg"
                />
              </Show>
            </div>

            <div class="flex items-start space-x-4 -mt-12 px-4">
              <div class="w-20 h-20 rounded-full bg-xcord-brand border-4 border-xcord-bg-secondary flex items-center justify-center text-white text-2xl font-semibold">
                <Show when={profileStore.userProfile!.avatarUrl} fallback={profileStore.userProfile!.username.charAt(0).toUpperCase()}>
                  <img
                    src={profileStore.userProfile!.avatarUrl}
                    alt={profileStore.userProfile!.username}
                    class="w-full h-full rounded-full object-cover"
                  />
                </Show>
              </div>
            </div>

            <div class="px-4 space-y-4">
              <Show when={!editMode()}>
                <div>
                  <h3 class="text-white font-semibold text-xl">{profileStore.userProfile!.displayName}</h3>
                  <p class="text-xcord-text-muted">@{profileStore.userProfile!.username}</p>
                </div>

                <Show when={profileStore.userProfile!.pronouns}>
                  <div>
                    <label class="text-xs text-xcord-text-muted">Pronouns</label>
                    <p class="text-white">{profileStore.userProfile!.pronouns}</p>
                  </div>
                </Show>

                <Show when={profileStore.userProfile!.bio}>
                  <div>
                    <label class="text-xs text-xcord-text-muted">Bio</label>
                    <p class="text-white whitespace-pre-wrap">{profileStore.userProfile!.bio}</p>
                  </div>
                </Show>

                <div>
                  <label class="text-xs text-xcord-text-muted">Member Since</label>
                  <p class="text-white">{new Date(profileStore.userProfile!.createdAt).toLocaleDateString()}</p>
                </div>
              </Show>

              <Show when={editMode()}>
                <div class="space-y-3">
                  <div>
                    <label class="text-xs text-xcord-text-muted block mb-1">Display Name</label>
                    <input
                      type="text"
                      class="w-full bg-xcord-bg-primary text-white px-3 py-2 rounded focus:outline-none focus:ring-2 focus:ring-xcord-brand"
                      value={displayName() || profileStore.userProfile!.displayName}
                      onInput={(e) => setDisplayName(e.currentTarget.value)}
                    />
                  </div>

                  <div>
                    <label class="text-xs text-xcord-text-muted block mb-1">Pronouns</label>
                    <input
                      type="text"
                      class="w-full bg-xcord-bg-primary text-white px-3 py-2 rounded focus:outline-none focus:ring-2 focus:ring-xcord-brand"
                      value={pronouns() || profileStore.userProfile!.pronouns || ''}
                      onInput={(e) => setPronouns(e.currentTarget.value)}
                      placeholder="e.g., they/them"
                    />
                  </div>

                  <div>
                    <label class="text-xs text-xcord-text-muted block mb-1">Bio</label>
                    <textarea
                      class="w-full bg-xcord-bg-primary text-white px-3 py-2 rounded focus:outline-none focus:ring-2 focus:ring-xcord-brand resize-none"
                      rows={4}
                      value={bio() || profileStore.userProfile!.bio || ''}
                      onInput={(e) => setBio(e.currentTarget.value)}
                      placeholder="Tell us about yourself..."
                    />
                  </div>

                  <button
                    class="w-full bg-xcord-brand text-white py-2 rounded hover:bg-xcord-brand/80 transition"
                    onClick={handleSaveUserProfile}
                  >
                    Save Changes
                  </button>
                </div>
              </Show>

              <PasswordChangeForm />
              <TwoFactorSetup />
              <AccountDeletion
                scheduledDeletionAt={profileStore.userProfile!.scheduledDeletionAt ?? null}
              />
            </div>
          </div>
        </Show>

        <Show when={props.serverId && profileStore.getServerProfile(props.serverId)}>
          <div class="space-y-4">
            <Show when={!editMode()}>
              <div>
                <label class="text-xs text-xcord-text-muted">Server Nickname</label>
                <p class="text-white">
                  {profileStore.getServerProfile(props.serverId!)?.nickname || 'No nickname set'}
                </p>
              </div>
            </Show>

            <Show when={editMode()}>
              <div class="space-y-3">
                <div>
                  <label class="text-xs text-xcord-text-muted block mb-1">Server Nickname</label>
                  <input
                    type="text"
                    class="w-full bg-xcord-bg-primary text-white px-3 py-2 rounded focus:outline-none focus:ring-2 focus:ring-xcord-brand"
                    value={nickname() || profileStore.getServerProfile(props.serverId!)?.nickname || ''}
                    onInput={(e) => setNickname(e.currentTarget.value)}
                    placeholder="Enter server nickname..."
                  />
                </div>

                <button
                  class="w-full bg-xcord-brand text-white py-2 rounded hover:bg-xcord-brand/80 transition"
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
