# StackPilot — Desktop App Design

## Framework: Tauri 2

Cross-platform desktop app using Tauri (Rust backend + WebView frontend sharing the Next.js/React component library).

## Architecture

```
┌─────────────────────────────────────┐
│         Tauri Shell (Rust)          │
│  ┌───────────────────────────────┐  │
│  │   WebView (StackPilot UI)     │  │
│  │   Shared React components     │  │
│  └───────────────────────────────┘  │
│  Native Modules:                    │
│  - Local repo indexer (git2-rs)     │
│  - Credential store (keyring crate) │
│  - File system watcher (notify)     │
│  - Native notifications             │
│  - Background scan scheduler        │
└─────────────────────────────────────┘
```

## Desktop-Specific Capabilities

| Capability | Implementation |
|------------|----------------|
| Local repo indexing | git2-rs clone/fetch, local file scan without API round-trip |
| Secure credential storage | OS keychain via `keyring` crate (Keychain/Credential Manager/libsecret) |
| Native notifications | Tauri notification plugin for approval requests, scan completion |
| Offline documentation | IndexedDB cache of documentation pages |
| Local architecture graph | SQLite cache of graph nodes/edges |
| Git branch awareness | git2-rs branch detection, sync with cloud on reconnect |
| Background scanning | Tauri background task scheduler |
| Multi-monitor layout | Detachable panels, pop-out architecture canvas |
| Deep links | `stackpilot://tickets/{id}`, `stackpilot://repos/{id}` |

## Project Structure (Future)

```
desktop/
  src-tauri/
    src/
      main.rs
      commands/
        repo_indexer.rs
        credential_store.rs
        file_watcher.rs
    Cargo.toml
    tauri.conf.json
  src/           # Shared or symlinked from frontend/
```

## Sync Strategy

- Online: Full API sync with cloud StackPilot instance
- Offline: Local SQLite cache, queue changes for sync on reconnect
- Conflict resolution: Server-wins for graph data, merge for local scan results

## Security

- Credentials never stored in WebView localStorage
- All secrets in OS keychain
- Certificate pinning for API communication
- Auto-lock after inactivity
