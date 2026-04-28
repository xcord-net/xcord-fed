import styles from './UpdatesTab.module.css';

export interface AvailableVersion {
  id: string;
  version: string;
  image: string;
  releaseNotes: string | null;
  isMinimumVersion: boolean;
  minimumEnforcementDate: string | null;
  publishedAt: string;
}

export interface UpgradeHistoryEntry {
  id: string;
  status: string;
  previousVersion: string | null;
  newVersion: string | null;
  targetImage: string;
  errorMessage: string | null;
  startedAt: string | null;
  completedAt: string | null;
}

export interface SystemVersionResponse {
  currentVersion: string;
  hubConnected: boolean;
  batchUpgradesEnabled: boolean;
  availableVersions: AvailableVersion[] | null;
  upgradeHistory: UpgradeHistoryEntry[] | null;
}

export interface ReleaseNotes {
  version: string;
  features: { summary: string; commit: string }[];
  fixes: { summary: string; commit: string }[];
  other: { summary: string; commit: string; type: string }[];
  breakingChanges: string[];
  migrationNotes: string;
  knownIssues: string;
}

export function parseReleaseNotes(raw: string | null): ReleaseNotes | null {
  if (!raw) return null;
  try {
    return JSON.parse(raw) as ReleaseNotes;
  } catch {
    return null;
  }
}

export function formatDate(dateStr: string | null): string {
  if (!dateStr) return '-';
  const d = new Date(dateStr);
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

export function formatDateTime(dateStr: string | null): string {
  if (!dateStr) return '-';
  const d = new Date(dateStr);
  return d.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function statusClass(status: string): string {
  const s = status.toLowerCase();
  if (s === 'completed' || s === 'success') return styles.historyStatusCompleted;
  if (s === 'failed' || s === 'error') return styles.historyStatusFailed;
  if (s === 'inprogress' || s === 'running') return styles.historyStatusInProgress;
  return styles.historyStatusDefault;
}
