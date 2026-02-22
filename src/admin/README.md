# Xcord Instance Admin SPA

Instance administration panel for xcord-fed. Built with SolidJS and Vite.

## Features

- Admin authentication with JWT claim validation
- Bot management (list bots, create tokens, revoke tokens)
- Webhook overview (list webhooks per server)
- Instance statistics dashboard

## Development

```bash
npm install
npm run dev
```

Dev server runs on port 3003 and proxies `/api` to `http://localhost:5041`.

## Build

```bash
npm run build
```

Output is written to `dist/`.

## Authentication

Admin users must have the `admin` claim set to `"true"` in their JWT. Non-admin users are rejected at login and on token validation.
