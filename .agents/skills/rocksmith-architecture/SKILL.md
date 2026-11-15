---
name: rocksmith-architecture
description: Modify Rocksmith CDLC Manager's desktop, library, queue, native messaging, or release pipeline while preserving the shared data and package contracts.
---

Read [architecture.md](../../../docs/architecture.md) before touching state or browser integration. Read [releases.md](../../../docs/releases.md) for packages or GitHub Actions.

- Keep durable state and archive operations in `Library.cs`. Keeping the state path and JSON shape allows an upgrade from earlier packages.
- Preserve the stable Ignition ID through queue, association, and the Installed badge. Derive Installed from the linked file on disk. A filename-only import does not imply a catalog match.
- When changing sync, exercise browser-only saves, reconnect, remove, and a linked file disappearing. Keep Chrome signed links out of persistent state.
- Build assets from source with `Package.ps1`. Test the extracted release layout when changing resource paths, versioning, or native messaging setup.
