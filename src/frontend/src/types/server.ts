export interface Server {
  id: string;
  name: string;
  description?: string;
  iconUrl?: string;
  bannerUrl?: string;
  ownerId: string;
  createdAt: string;
  /**
   * The channel to open when arriving at this server with nothing more specific
   * in mind. Only the create-server response carries it; the list endpoint does
   * not, which is why it is optional.
   */
  systemChannelId?: string;
}
