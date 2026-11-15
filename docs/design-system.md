# Design system

Rocksmith+ informs the high-contrast charcoal and yellow palette. The mark in the sidebar and executable is specific to CDLC Manager.

| Role | Value |
| --- | --- |
| Window | `#101112` |
| Sidebar | `#151719` |
| Surface | `#191B1D` |
| Border | `#343739` |
| Main text | `#F5F5EE` |
| Secondary text | `#A8ADB0` |
| Accent | `#F6E650` |

The desktop uses Segoe UI, 14 DIP body text, 31 DIP page titles, 40 DIP inputs and buttons, and 48 DIP minimum list rows. Controls and panels have square corners. Yellow marks the main action and keyboard focus. The current navigation button uses an amber-tinted surface.

The left sidebar and custom caption stay fixed. WPF WindowChrome supplies the drag and resize areas. Keep the title and status rows in place when moving between views. Use the center for files or songs and reserve the footer for actions on that selection. Keep settings grouped by the setting being changed.

Maintain the mark in `desktop/Shell.xaml` and `scripts/New-Icons.ps1` together. Packaging renders the Windows ICO and the Chrome PNGs from the same contours.

