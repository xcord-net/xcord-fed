// -- Types ------------------------------------------------------------------

export type TriggerType = 'Keyword' | 'Regex' | 'MentionSpam' | 'MessageSpam' | 'LinkFilter';
export type ActionType = 'DeleteMessage' | 'TimeoutUser' | 'AlertMods' | 'BlockMessage';

export interface AutomodRule {
  id: string;
  serverId: string;
  name: string;
  enabled: boolean;
  triggerType: TriggerType;
  triggerConfig: string;
  actionType: ActionType;
  actionConfig: string | null;
  exemptRoleIds: string | null;
  exemptChannelIds: string | null;
  exemptBots: boolean;
  createdAt: string;
}

export const TRIGGER_LABELS: Record<TriggerType, string> = {
  Keyword: 'Keyword Filter',
  Regex: 'Regex Pattern',
  MentionSpam: 'Mention Spam',
  MessageSpam: 'Message Spam',
  LinkFilter: 'Link Filter',
};

export const ACTION_LABELS: Record<ActionType, string> = {
  DeleteMessage: 'Delete Message',
  TimeoutUser: 'Timeout User',
  AlertMods: 'Alert Moderators',
  BlockMessage: 'Block Message',
};

export const TRIGGER_TYPES: TriggerType[] = [
  'Keyword',
  'Regex',
  'MentionSpam',
  'MessageSpam',
  'LinkFilter',
];
export const ACTION_TYPES: ActionType[] = [
  'BlockMessage',
  'DeleteMessage',
  'TimeoutUser',
  'AlertMods',
];

// -- Default trigger config helpers -----------------------------------------

export function defaultTriggerConfig(type: TriggerType): string {
  switch (type) {
    case 'Keyword':
      return JSON.stringify({ keywords: [], matchWholeWord: false });
    case 'Regex':
      return JSON.stringify({ pattern: '', caseSensitive: false });
    case 'MentionSpam':
      return JSON.stringify({ maxMentions: 5 });
    case 'MessageSpam':
      return JSON.stringify({ maxMessages: 5, intervalSeconds: 10 });
    case 'LinkFilter':
      return JSON.stringify({ blockedDomains: [], allowedDomains: [] });
  }
}

// -- Trigger config display --------------------------------------------------

export function triggerConfigSummary(type: TriggerType, config: string): string {
  try {
    const parsed = JSON.parse(config);
    switch (type) {
      case 'Keyword':
        return (parsed.keywords as string[])?.join(', ') || '(no keywords)';
      case 'Regex':
        return parsed.pattern || '(no pattern)';
      case 'MentionSpam':
        return `max ${parsed.maxMentions} mentions`;
      case 'MessageSpam':
        return `max ${parsed.maxMessages} msgs / ${parsed.intervalSeconds}s`;
      case 'LinkFilter': {
        const blocked: string[] = parsed.blockedDomains ?? [];
        const allowed: string[] = parsed.allowedDomains ?? [];
        if (blocked.length > 0) return `blocked: ${blocked.join(', ')}`;
        if (allowed.length > 0) return `allowed: ${allowed.join(', ')}`;
        return '(no domains configured)';
      }
    }
  } catch {
    return config;
  }
}
