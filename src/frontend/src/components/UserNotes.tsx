import { Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';

// ---- Types ----

interface UserLookupResult {
  id: string;
  username: string;
  displayName?: string;
}

interface UserNoteDto {
  id: string;
  targetUserId: string;
  content: string;
  createdAt: string;
  updatedAt?: string;
}

// ---- Component ----

export default function UserNotes() {
  const [searchUsername, setSearchUsername] = createSignal('');
  const [lookedUpUser, setLookedUpUser] = createSignal<UserLookupResult | null>(null);
  const [lookupError, setLookupError] = createSignal<string | null>(null);
  const [isLooking, setIsLooking] = createSignal(false);

  const [noteContent, setNoteContent] = createSignal('');
  const [existingNote, setExistingNote] = createSignal<UserNoteDto | null>(null);
  const [isSaving, setIsSaving] = createSignal(false);
  const [isDeleting, setIsDeleting] = createSignal(false);
  const [saveError, setSaveError] = createSignal<string | null>(null);
  const [saveSuccess, setSaveSuccess] = createSignal(false);
  const [deleteSuccess, setDeleteSuccess] = createSignal(false);

  const lookupUser = async () => {
    const username = searchUsername().trim();
    if (!username) return;
    setIsLooking(true);
    setLookupError(null);
    setLookedUpUser(null);
    setExistingNote(null);
    setNoteContent('');
    setSaveSuccess(false);
    setDeleteSuccess(false);
    try {
      const user = await api.get<UserLookupResult>(`/api/v1/users/by-username/${username}`);
      setLookedUpUser(user);
      // Try to load existing note for this user
      try {
        const note = await api.get<UserNoteDto>(`/api/v1/users/${user.id}/notes`);
        setExistingNote(note);
        setNoteContent(note.content);
      } catch {
        // No note yet — that's fine
      }
    } catch {
      setLookupError('User not found. Check the username and try again.');
    } finally {
      setIsLooking(false);
    }
  };

  const saveNote = async () => {
    const user = lookedUpUser();
    if (!user) return;
    const content = noteContent().trim();
    if (!content) return;
    setIsSaving(true);
    setSaveError(null);
    setSaveSuccess(false);
    try {
      const note = await api.put<UserNoteDto>(`/api/v1/users/${user.id}/notes`, { content });
      setExistingNote(note);
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 2500);
    } catch {
      setSaveError('Failed to save note. Please try again.');
    } finally {
      setIsSaving(false);
    }
  };

  const deleteNote = async () => {
    const user = lookedUpUser();
    if (!user) return;
    setIsDeleting(true);
    setSaveError(null);
    setDeleteSuccess(false);
    try {
      await api.delete(`/api/v1/users/${user.id}/notes`);
      setExistingNote(null);
      setNoteContent('');
      setDeleteSuccess(true);
      setTimeout(() => setDeleteSuccess(false), 2500);
    } catch {
      setSaveError('Failed to delete note. Please try again.');
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <div class="flex flex-col bg-xcord-bg-secondary">
      {/* Header */}
      <div class="px-4 py-3 border-b border-xcord-bg-tertiary">
        <h2 class="text-xcord-text-primary font-semibold">User Notes</h2>
        <p class="text-xcord-text-muted text-xs mt-0.5">
          Add private notes about other users. Only you can see them.
        </p>
      </div>

      {/* User lookup */}
      <div class="px-4 py-3 border-b border-xcord-bg-tertiary">
        <label class="text-xs text-xcord-text-muted block mb-1.5">Look up user by username</label>
        <div class="flex gap-2">
          <input
            id="note-username-input"
            type="text"
            placeholder="Enter username..."
            class="flex-1 bg-xcord-bg-primary text-xcord-text-primary px-3 py-2 rounded text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand"
            value={searchUsername()}
            onInput={(e) => setSearchUsername(e.currentTarget.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') lookupUser(); }}
          />
          <button
            class="bg-xcord-brand text-white px-3 py-2 rounded text-sm font-medium hover:bg-xcord-brand/80 transition-colors disabled:opacity-50"
            onClick={lookupUser}
            disabled={isLooking() || !searchUsername().trim()}
          >
            {isLooking() ? 'Looking...' : 'Find'}
          </button>
        </div>
        <Show when={lookupError()}>
          <p class="text-red-400 text-xs mt-1">{lookupError()}</p>
        </Show>
      </div>

      {/* Note editor — shown once a user is found */}
      <Show when={lookedUpUser()}>
        {(user) => (
          <div class="px-4 py-3">
            <div class="mb-3">
              <p class="text-xcord-text-primary font-medium text-sm">{user().displayName ?? user().username}</p>
              <p class="text-xcord-text-muted text-xs">@{user().username}</p>
              <Show when={existingNote()}>
                <p class="text-xcord-text-muted text-xs mt-0.5">
                  Note last updated{' '}
                  {new Date(existingNote()!.updatedAt ?? existingNote()!.createdAt).toLocaleDateString()}
                </p>
              </Show>
            </div>

            <label class="text-xs text-xcord-text-muted block mb-1.5">
              {existingNote() ? 'Edit note' : 'Add a note'}
            </label>
            <textarea
              id="note-content-input"
              class="w-full bg-xcord-bg-primary text-xcord-text-primary px-3 py-2 rounded text-sm focus:outline-none focus:ring-2 focus:ring-xcord-brand resize-none"
              rows={4}
              placeholder="Write a private note about this user..."
              value={noteContent()}
              onInput={(e) => setNoteContent(e.currentTarget.value)}
              aria-label="Note content"
            />

            <Show when={saveError()}>
              <p class="text-red-400 text-xs mt-1">{saveError()}</p>
            </Show>

            <div class="flex items-center gap-2 mt-2">
              <button
                class="bg-xcord-brand text-white px-4 py-1.5 rounded text-sm font-medium hover:bg-xcord-brand/80 transition-colors disabled:opacity-50"
                onClick={saveNote}
                disabled={isSaving() || !noteContent().trim()}
              >
                {isSaving() ? 'Saving...' : existingNote() ? 'Update Note' : 'Save Note'}
              </button>

              <Show when={existingNote()}>
                <button
                  class="bg-xcord-bg-tertiary text-red-400 px-4 py-1.5 rounded text-sm font-medium hover:bg-red-600 hover:text-white transition-colors disabled:opacity-50"
                  onClick={deleteNote}
                  disabled={isDeleting()}
                >
                  {isDeleting() ? 'Deleting...' : 'Delete Note'}
                </button>
              </Show>

              <Show when={saveSuccess()}>
                <span class="text-green-400 text-sm">Saved!</span>
              </Show>
              <Show when={deleteSuccess()}>
                <span class="text-green-400 text-sm">Note deleted.</span>
              </Show>
            </div>
          </div>
        )}
      </Show>
    </div>
  );
}
