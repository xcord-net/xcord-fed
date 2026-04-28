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

export interface CreateBotResponse {
  id: string;
  username: string;
  displayName: string;
  token: string;
  tokenId: string;
}
