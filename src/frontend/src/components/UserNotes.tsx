import { Show, createSignal, onMount } from 'solid-js';
import { api } from '../api/client';
import Flexbox from './ui/Flexbox';
import styles from './UserNotes.module.css';

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
        // No note yet - that's fine
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
    <Flexbox direction="vertical" class={styles.container}>
      {/* Header */}
      <div class={styles.header}>
        <h2 class={styles.heading}>User Notes</h2>
        <p class={styles.headerSubtext}>
          Add private notes about other users. Only you can see them.
        </p>
      </div>

      {/* User lookup */}
      <div class={styles.lookupSection}>
        <label class={styles.lookupLabel}>Look up user by username</label>
        <Flexbox gap={0.5}>
          <input
            id="note-username-input"
            type="text"
            placeholder="Enter username..."
            class={styles.searchInput}
            value={searchUsername()}
            onInput={(e) => setSearchUsername(e.currentTarget.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') lookupUser(); }}
          />
          <button
            id="user-notes-find-btn"
            class={styles.findButton}
            onClick={lookupUser}
            disabled={isLooking() || !searchUsername().trim()}
          >
            {isLooking() ? 'Looking...' : 'Find'}
          </button>
        </Flexbox>
        <Show when={lookupError()}>
          <p class={styles.lookupError}>{lookupError()}</p>
        </Show>
      </div>

      {/* Note editor - shown once a user is found */}
      <Show when={lookedUpUser()}>
        {(user) => (
          <div class={styles.editorSection}>
            <div class={styles.userInfo}>
              <p class={styles.displayName}>{user().displayName ?? user().username}</p>
              <p class={styles.usernameHandle}>@{user().username}</p>
              <Show when={existingNote()}>
                <p class={styles.noteUpdatedAt}>
                  Note last updated{' '}
                  {new Date(existingNote()!.updatedAt ?? existingNote()!.createdAt).toLocaleDateString()}
                </p>
              </Show>
            </div>

            <label class={styles.noteLabel}>
              {existingNote() ? 'Edit note' : 'Add a note'}
            </label>
            <textarea
              id="note-content-input"
              class={styles.noteTextarea}
              rows={4}
              placeholder="Write a private note about this user..."
              value={noteContent()}
              onInput={(e) => setNoteContent(e.currentTarget.value)}
              aria-label="Note content"
            />

            <Show when={saveError()}>
              <p class={styles.saveError}>{saveError()}</p>
            </Show>

            <Flexbox align="center" gap={0.5} class={styles.actionRow}>
              <button
                id="user-notes-save-btn"
                class={styles.saveButton}
                onClick={saveNote}
                disabled={isSaving() || !noteContent().trim()}
              >
                {isSaving() ? 'Saving...' : existingNote() ? 'Update Note' : 'Save Note'}
              </button>

              <Show when={existingNote()}>
                <button
                  id="user-notes-delete-btn"
                  class={styles.deleteButton}
                  onClick={deleteNote}
                  disabled={isDeleting()}
                >
                  {isDeleting() ? 'Deleting...' : 'Delete Note'}
                </button>
              </Show>

              <Show when={saveSuccess()}>
                <span id="user-notes-save-status" class={styles.saveSuccessText}>Saved!</span>
              </Show>
              <Show when={deleteSuccess()}>
                <span id="user-notes-delete-status" class={styles.deleteSuccessText}>Note deleted.</span>
              </Show>
            </Flexbox>
          </div>
        )}
      </Show>
    </Flexbox>
  );
}
