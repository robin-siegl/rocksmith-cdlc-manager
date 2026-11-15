---
name: rocksmith-design-system
description: Design or revise Rocksmith CDLC Manager screens, controls, navigation, branding, or its Chrome popup using the project's shared visual patterns.
---

Read [design-system.md](../../../docs/design-system.md) for the palette, control sizes, layouts, and logo sources.

- Keep the sidebar and title region mounted when switching sections. Load disk-backed lists off the UI thread and commit results only to the view that requested them.
- Apply the WPF input and button styles from `desktop/Shell.xaml` before changing local dimensions. Put the action beside its input or below its list.
- Name actions for the user's next step. Show song-specific actions after selection and keep their layout space reserved.
- Check the empty and populated views, keyboard focus, and the minimum window size. For a navigation edit, switch views while a scan is running.
