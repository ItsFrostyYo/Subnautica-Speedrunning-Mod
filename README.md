# Subnautica Speedrunning Ranked Mod

Public repo root for the ranked Subnautica September 2018 client.

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
.\Launcher\publish-to-game.ps1 -BuildFirst
```

## Install Shape

After packaging, the game folder can be launched either with:

- `Launch Ranked.exe`
- `SubnauticaSpeedrunningRanked\Launch Ranked.exe`

Both resolve the same ranked runtime folder.

## Branch Flow

- `development`
  Active mod development branch.
- `main`
  Stable public branch intended for release packaging.

See [Repository Workflow](Docs/Reference/Repository Workflow.md) for the full branch, release, and install/update flow.
