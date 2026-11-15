# Architecture

The desktop uses WPF XAML on .NET Framework 4.8. `desktop/Shell.xaml` owns layout and styles. `desktop/Manager.cs` wires user actions, runs scans and imports off the UI thread, and refreshes the active view. Version checks discard results from views that the user has left.

`desktop/Library.cs` owns the JSON store and PSARC operations. The store lives under `%LOCALAPPDATA%\RocksmithCdlcManager`. A per-user mutex coordinates the desktop and short-lived browser bridge processes. Updates replace the JSON file atomically. Imports stage into a temporary destination file, check the PSARC header, and preserve any different file already at the destination.

Chrome’s Manifest V3 worker stores queued changes locally and sends them to `local.rocksmith_cdlc_manager` when it connects. Chrome launches the same executable with the extension origin, which selects the native messaging entry point. Only the extension ID entered in Settings is authorized by the host manifest. Content scripts render controls against Ignition listing IDs and keep the signed-in browser session on Ignition.

The bridge handles `save`, `remove`, and `status`. Song records use the Ignition ID, title, canonical URL, and optional file path relative to the configured DLC folder. The status response returns pending songs and installed IDs. The app keeps installed status separate from generic filename imports and checks that linked files still exist.

`ArrangementIndex.cs` reads a local CustomsForge Song Manager Arrangement Analyzer export saved as `arrangements.csv` in the app data folder. It matches full paths before trying an unambiguous filename. Library grouping preserves the tuning of each arrangement. Files with missing or ambiguous metadata remain visible as unknown. Importing an export replaces the saved metadata snapshot. The library reads metadata from the CSV and checks installed file existence through its normal folder scan.

