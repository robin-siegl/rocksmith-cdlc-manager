# Build and release

On Windows x64, run `./Package.ps1`. The script compiles the portable executable, generates Windows and Chrome icons, and writes the Windows ZIP, Chrome ZIP, and `SHA256SUMS.txt` to `dist`. It uses the .NET Framework compiler included with Windows and has no NuGet dependency.

The GitHub Actions workflow builds and tests the packages for pushes to `main`, pull requests, and version tags. Every successful run exposes downloadable artifacts. A push to `main` publishes a release if that version does not already exist. A matching version tag can also trigger publication. To retry from the web UI, run **Build and release** against `main` and enable **Publish a release**.

For a new release:

1. Update `VERSION` and `extension/manifest.json` to the same three-part version.
2. Merge the changes to `main`.
3. Check **Build and release** in the Actions tab.
4. Verify the release assets and the checksums. Extract and run the Windows ZIP and load the Chrome ZIP as an unpacked extension.

The published Windows binary is unsigned. The Chrome archive is for manual loading and uses the ID shown by the browser on that machine.
