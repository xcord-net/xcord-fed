/** Matches BotTokenDto from the backend */
export interface BotTokenDto {
  tokenId: string;
  tokenName: string;
  userId: string;
  username: string;
  permissions: number;
  isRevoked: boolean;
  createdAt: string;
  lastUsedAt: string | null;
}

/** Matches ListBotsResponse from the backend */
export interface ListBotsResponse {
  bots: BotTokenDto[];
}

/** Matches CreateBotCommand - sent as POST /api/v1/admin/bots */
export interface CreateBotRequest {
  username: string;
  displayName: string;
  tokenName: string;
  permissions: number;
}

/** Matches CreateBotResponse from the backend */
export interface CreateBotResponse {
  userId: string;
  username: string;
  displayName: string;
  tokenId: string;
  tokenName: string;
  rawToken: string;
  permissions: number;
  createdAt: string;
}

/** Matches CreateBotTokenRequest from the backend */
export interface CreateBotTokenRequest {
  tokenName: string;
  permissions: number;
}

/** Matches CreateBotTokenResponse from the backend */
export interface CreateBotTokenResponse {
  tokenId: string;
  tokenName: string;
  rawToken: string;
  permissions: number;
  createdAt: string;
}

/** Matches BotTokenMetadataDto from ListBotTokensHandler */
export interface BotTokenMetadataDto {
  tokenId: string;
  tokenName: string;
  permissions: number;
  isRevoked: boolean;
  createdAt: string;
  lastUsedAt: string | null;
}

/** Matches ListBotTokensResponse from the backend */
export interface ListBotTokensResponse {
  tokens: BotTokenMetadataDto[];
}

/** Matches DeleteBotResponse from the backend */
export interface DeleteBotResponse {
  botId: string;
  success: boolean;
}
