// Shared types for the BotsTab component tree.

export interface AgentParameterManifest {
  name: string;
  type: 'string' | 'number' | 'boolean';
  description: string | null;
  required: boolean;
  defaultValue: string | null;
}

export interface AgentManifest {
  name: string;
  description: string | null;
  category: string | null;
  parameters: AgentParameterManifest[];
}

export interface BotAgent {
  id: string;
  name: string;
  description: string | null;
  category: string | null;
  manifest: AgentManifest | null;
}

export interface BotToken {
  id: string;
  name: string;
  tokenHash: string;
  createdAt: string;
  lastUsedAt: string | null;
}

/** One row of `GET /api/v1/admin/bots`: a token, plus the bot it belongs to. */
export interface BotTokenRow {
  tokenId: string;
  tokenName: string;
  tokenHash?: string;
  userId: string;
  username: string;
  displayName?: string;
  roles: number;
  isRevoked: boolean;
  createdAt: string;
  lastUsedAt: string | null;
}

export interface Bot {
  id: string;
  username: string;
  displayName: string;
  agentId: string | null;
  agentName: string | null;
  isRunning: boolean;
  agentConfigJson: string | null;
  tokens: BotToken[];
}

/**
 * What `POST /api/v1/admin/bots` actually returns.
 *
 * The names matter more than usual here: the raw token is shown once and never
 * again, so reading a field the server does not send loses it for good. This
 * type claimed `id` and `token`; the server sends `userId` and `rawToken`.
 */
export interface CreateBotResponse {
  userId: string;
  username: string;
  displayName: string;
  rawToken: string;
  tokenId: string;
  tokenName: string;
  roles: number;
  createdAt: string;
}
