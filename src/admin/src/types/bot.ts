export interface Bot {
  id: string;
  name: string;
  discriminator: string;
  avatarUrl?: string;
  createdAt: string;
}

export interface BotToken {
  id: string;
  botId: string;
  tokenName: string;
  permissions: number;
  token?: string;
  createdAt: string;
  lastUsedAt?: string;
}

export interface CreateBotTokenRequest {
  tokenName: string;
  permissions: number;
}
