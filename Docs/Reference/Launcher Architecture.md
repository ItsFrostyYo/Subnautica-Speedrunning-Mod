# Launcher Architecture

Current public beta: `Beta-0.3.1`

This document is the technical note for the ranked loading stack.

## Components

`SubnauticaSpeedrunningRanked\Launch Ranked.exe`

- validates the game install
- prepares ranked folders
- manages `doorstop_config.ini`
- starts `Subnautica.exe`
- checks the repo `release/` manifest for updates

`SubnauticaSpeedrunningRanked.Bootstrap.dll`

- loaded by Doorstop beside `Subnautica.exe`
- re-validates the supported game build
- loads the ranked runtime

`SubnauticaSpeedrunningRanked.Runtime.dll`

- owns logging, runtime systems, UI, seeds, timer logic, and future networking

`SubnauticaSpeedrunningRanked\Updater\Ranked Updater.exe`

- applies package updates out of process
- relaunches Ranked after update

## Deployment Shape

```text
Game Folder Containing Subnautica.exe
  Subnautica.exe
  Launch Ranked.lnk
  winhttp.dll
  .doorstop_version
  doorstop_config.ini
  SubnauticaSpeedrunningRanked\
    Launch Ranked.exe
    Bootstrap\
    Runtime\
    Updater\
    Config\
    Logs\
    Modules\
    Cache\
    Data\
```

## Validation Rules

The client is locked to the clean official September 2018 speedrun build by checking the real game files and hashes, not by checking a specific folder name.

## Source Layout

- `Launcher\src\SubnauticaSpeedrunningRanked.Runtime\Core`
  Loader, validation, diagnostics, and runtime bootstrapping.
- `Launcher\src\SubnauticaSpeedrunningRanked.Runtime\Server`
  Future match transport and server/API call code.
- `Launcher\src\SubnauticaSpeedrunningRanked.Runtime\Seeds`
  Seed definitions, deterministic rolls, and spawn manipulation.
- `Launcher\src\SubnauticaSpeedrunningRanked.Runtime\UI`
  Main menu changes and persistent overlays.
- `Launcher\src\SubnauticaSpeedrunningRanked.Runtime\Speedrun`
  Timer logic, split detection, run state tracking, and creative-only helpers.
