# Publishing a Mod Update

The public Beta-0.10.1 launcher reads `release/latest.json` from the repository's `main` branch. A new ZIP on GitHub is not discovered until that manifest is also updated on `main`.

For a clean release:

1. Run `Build-Release.ps1` and confirm its package validation succeeds.
2. Commit the mod source, `release/latest.json`, and this release documentation.
3. Push the release commit to `development`.
4. Create and publish the GitHub release from that commit with the exact tag from the manifest, such as `Beta-0.11.1`.
5. Upload the exact ZIP named by `zipFileName` and confirm the asset URL in `latest.json` returns HTTP 200.
6. Only after the release asset is live, merge the release commit into `main` and push `main`. This publishes the new manifest to existing launchers.

Publishing the release asset before updating `main` prevents users from receiving an update prompt whose download does not exist yet.

The ZIP must contain the package files directly at its root. Do not wrap them in an additional version-named directory; the Beta-0.10.1 updater expects `SubnauticaSpeedrunningMod/Launch Mod.exe` directly inside the archive.
