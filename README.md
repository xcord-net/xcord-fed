# xcord-fed

Federation instance module for [Xcord](https://github.com/xcord-net). Contains the chat client SPA, backend API, and instance admin panel.

Each provisioned instance runs as an isolated Docker container from this module's image, with its own database, storage credentials, encryption keys, and JWT signing keys.

## Structure

```
xcord-fed/
├── src/
│   ├── backend/     # ASP.NET Core API + SignalR hub + EF Core (.NET 9)
│   ├── frontend/    # Chat client SPA (SolidJS + Vite + Tailwind)
│   └── admin/       # Instance admin SPA (SolidJS + Vite)
├── docker/          # Docker Compose for local dev infra
└── Dockerfile       # Multi-stage production image
```

## Backend

The backend uses vertical slice architecture with a unified SignalR hub (`/hubs/main`). Features include:

- Auth (register, login, 2FA, password management)
- Servers, channels, categories, roles, permission overrides
- Messages with reactions, embeds, attachments, pins, search
- DMs (1:1 and group), threads, forum channels
- Voice/video via LiveKit, polls, custom emoji, stickers
- Moderation (bans, timeouts, reports, audit log, automod)
- Webhooks (incoming), bots (simple token-based), federation

## Frontend

SolidJS SPA with signal-based stores for servers, channels, messages, presence, voice, and more. Communicates via REST (`/api/v1/`) and SignalR for real-time events.

## Running Tests

```bash
# Unit + integration tests (requires Docker for Testcontainers)
dotnet test src/backend/Xcord.sln --verbosity quiet

# Frontend type check + unit tests
cd src/frontend && npx tsc --noEmit && npx vitest run
```

## License

Apache 2.0 -- see [LICENSE](LICENSE).
