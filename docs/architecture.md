# Architecture

The desktop uses WPF XAML on .NET Framework 4.8. `desktop/Shell.xaml` owns layout and styles. `desktop/Manager.cs` wires user actions, runs scans and imports off the UI thread, and refreshes the active view. Version checks discard results from views that the user has left.

`desktop/Library.cs` owns the JSON store and PSARC operations. The store lives under `%LOCALAPPDATA%\RocksmithCdlcManager`. A per-user mutex coordinates the desktop and short-lived browser bridge processes. Updates replace the JSON file atomically. Imports stage into a temporary destination file, check the PSARC header, and preserve any different file already at the destination.

Chrome’s Manifest V3 worker stores queued changes locally and sends them to `local.rocksmith_cdlc_manager` when it connects. Chrome launches the same executable with the extension origin, which selects the native messaging entry point. Only the extension ID entered in Settings is authorized by the host manifest. Content scripts render controls against Ignition listing IDs and keep the signed-in browser session on Ignition.

The bridge handles `save`, `remove`, `downloaded`, and `status`. Chrome associates completed, extension-initiated PC downloads with an Ignition ID. The desktop associates an import only when its source path identifies one song. Queue removal retains records that have download or install state so the Installed badge can survive queue removal. Signed Ignition links are requested in the browser when needed.

Downloads cleanup compares contents and revalidates candidates before recycling them. It operates on PC archives at the top level of the configured Downloads folder. The installed library lists files from the DLC folder and its subfolders.

