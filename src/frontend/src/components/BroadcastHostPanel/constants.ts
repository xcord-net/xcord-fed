import type { BroadcastLayoutPreset } from '../../stores/broadcast.store';

export const LAYOUTS: { preset: BroadcastLayoutPreset; label: string; description: string }[] = [
  { preset: 'Grid', label: 'Grid', description: 'Equal tiles for all participants' },
  { preset: 'Spotlight', label: 'Spotlight', description: 'One large, others as thumbnails' },
  { preset: 'Pip', label: 'Picture-in-Picture', description: 'Main feed with small overlay' },
  { preset: 'SideBySide', label: 'Side by Side', description: 'Two equal panels' },
  { preset: 'AudioShow', label: 'Audio show', description: 'Voices only \u2014 nobody has to be on camera' },
];

// Number of stage slots shown for each preset.
export function slotCountFor(preset: BroadcastLayoutPreset): number {
  switch (preset) {
    case 'Grid': return 8;
    case 'Spotlight': return 6;
    case 'Pip': return 2;
    case 'SideBySide': return 2;
    // An audio show has no tiles to fill, so the stage is as big as the
    // conversation - capped where a panel of voices stops being followable.
    case 'AudioShow': return 8;
  }
}
