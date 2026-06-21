# Subnautica Speedrunning Ranked Mod

Public repo root for the ranked Subnautica September 2018 client.

Current public beta version: `Beta-0.3.0`

## Structure

- `Launcher/`
  Custom launcher, bootstrap, runtime, build scripts, and packaging flow.
- `Docs/`
  Mod-facing documentation that belongs with the public project.
- `release/`
  Generated release packages and zips via `Build-ReleasePackage.ps1`.

## Runtime Sections

- `Core`
  Loader, module bootstrapping, runtime context, diagnostics, validation.
- `Server`
  Server/API client and future match/server call code.
- `Seeds`
  Seed definitions, seed storage, seed runtime, and live spawn rules.
- `UI`
  Main menu overrides and persistent overlays.
- `Speedrun`
  Timer, split detection, run state detection, and creative consistency helpers.

## Build

From this repo root:

```powershell
.\Build-ReleasePackage.ps1 -BuildFirst
```

For direct game publishing during local testing:

```powershell
.\Launcher\publish-to-game.ps1 -BuildFirst -CreateShortcut
```

## Install Shape

The launcher lives inside `SubnauticaSpeedrunningRanked`.

After the first launch, the client creates a convenience shortcut in the game root:

- `Launch Ranked.lnk`
- target: `SubnauticaSpeedrunningRanked\Launch Ranked.exe`

The root game folder should not contain the ranked launcher binaries anymore.

## Update Source

The in-client updater reads from the repository `main` branch `release/` folder:

- `release/latest.json`
- `release/SubnauticaSpeedrunningRanked-Beta-0.3.0.zip`

## Branch Flow

- `development`
  Active mod development branch.
- `main`
  Stable public branch intended for release packaging.

See [Repository Workflow](Docs/Reference/Repository Workflow.md) for the full branch, release, and install/update flow.
