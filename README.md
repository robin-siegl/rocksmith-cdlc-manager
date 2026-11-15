# Rocksmith CDLC Manager

A Windows desktop app for your Rocksmith 2014 custom DLC and a Chrome extension for the Ignition download queue.

## Get started

Download both ZIPs from [GitHub Releases](https://github.com/robin-siegl/rocksmith-cdlc-manager/releases). Extract them to folders you plan to keep.

1. Open **Rocksmith CDLC Manager.exe** from the Windows ZIP. In **Settings**, choose the game's `dlc` folder and the folder where Chrome saves downloads. Steam can find the game folder under **Rocksmith 2014 → Manage → Browse local files**.
2. Visit `chrome://extensions`, turn on **Developer mode**, select **Load unpacked**, and choose the extracted Chrome package folder. Copy the extension ID from the extension card.
3. Paste that ID into **Settings → Chrome connection** in the app and select **Connect Chrome**. Reload any Ignition tabs that are already open.

The Windows release is a portable x64 application for Windows 10/11 with .NET Framework 4.8. The Chrome package is an unpacked extension.

## Use it

- **Save a song.** Select **Save for later** on Ignition. Find saved songs in the extension’s **Download Later** popup and in the desktop **Download queue**. The extension can hold saves locally until it connects.
- **Download and link.** Open a queued song in Ignition, download the Windows file, then select the song in the app and choose **Import for song**. The extension shows **Installed** while its linked file exists. Use **Link existing file** for a song already in your DLC folder.
- **Import several files.** In **Import files**, select PC archives from Downloads and click **Import selected**. The app keeps the originals, reuses identical same-name files, and gives different files a new name.
- **Browse local files.** **Installed library** lists PC CDLC in the DLC folder and its subfolders. Use **Show in Explorer** to locate a file.
- **See tunings and arrangements.** In CustomsForge Song Manager's **Arrangement Analyzer**, enable **FilePath** or **FileName**, **Arrangement**, and **Tuning**, plus **Artist** and **Title** for song names. Export the grid as CSV and use **Installed library → Import arrangement CSV**. Group by tuning or by arrangement to see Lead, Rhythm, and Bass separately. Export again after adding songs to update the metadata.
- **Paste links.** Ignition song links are queued. A direct HTTPS link serving a PC PSARC can be downloaded in the app. Share pages that require a confirmation or file choice use Chrome.

## Develop

Use PowerShell on Windows x64 to run `./Build.ps1` or `./Package.ps1`. Load the generated Chrome package rather than the source folder so the icons are available.

- [Architecture](docs/architecture.md)
- [Design system](docs/design-system.md)
- [Build and release](docs/releases.md)
- [Project instructions](AGENTS.md)

Independent of Ubisoft and CustomsForge.

