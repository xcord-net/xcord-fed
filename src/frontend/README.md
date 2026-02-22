# Xcord Frontend Client

SolidJS + Vite + TypeScript + Tailwind CSS chat client.

## Development

```bash
# Install dependencies
npm install

# Start dev server (port 3000)
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview
```

## Project Structure

```
src/
├── api/          # API client for backend communication
├── stores/       # Global state management (auth, etc.)
├── pages/        # Route components (Login, Register, Home)
├── types/        # TypeScript type definitions
├── App.tsx       # Router configuration
├── index.tsx     # Application entry point
└── index.css     # Tailwind CSS imports
```

## Tech Stack

- **SolidJS** - Reactive UI framework
- **@solidjs/router** - Client-side routing
- **TypeScript** - Type safety
- **Tailwind CSS v4** - Styling with @tailwindcss/vite plugin
- **Vite** - Build tool and dev server

## API Proxy

Dev server proxies backend requests:
- `/api/*` -> `http://localhost:5041`
- `/hubs/*` -> `http://localhost:5041` (WebSocket support)

## Notes

- All IDs are strings (serialized bigints from backend)
- SolidJS uses `class=` not `className=`
- Auth tokens stored in localStorage with automatic refresh
- API client handles 401 responses with token refresh retry
