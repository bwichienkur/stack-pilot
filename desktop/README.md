# StackPilot Desktop (Tauri)

Native desktop shell for the StackPilot web application.

## Prerequisites

- [Rust](https://rustup.rs/)
- Node.js 20+
- Frontend dev server or production build

## Development

```bash
# Terminal 1: frontend
cd frontend && npm run dev

# Terminal 2: Tauri (loads http://localhost:3000)
cd desktop && npm run dev
```

## Production Build

```bash
cd frontend && npm run build && npm run start
cd desktop && npm run build
```

## Configuration

Edit `src-tauri/tauri.conf.json` to point `devUrl` / `frontendDist` at your StackPilot frontend.

The desktop app is a thin Tauri wrapper—all business logic lives in the API and Next.js frontend.
