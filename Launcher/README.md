# Subnautica Speedrunning Ranked Launcher

This folder contains the first custom loading stack for the ranked mod.

The design goal is:

- custom launcher
- custom bootstrap
- custom runtime
- custom module system
- strong logging and diagnostics
- exact game-version validation and lockout
- crash reports and failure diagnostics
- future-ready config and network hooks
- no dependency on BepInEx as the mod loader

## Architecture

`Launch Ranked.exe`

- Windows launcher that can live in `SubnauticaSpeedrunningRanked`
- or be copied directly beside `Subnautica.exe`
- validates the game install
- prepares folder layout
- manages `doorstop_config.ini`
- launches `Subnautica.exe`

`SubnauticaSpeedrunningRanked.Bootstrap.dll`

- very small bootstrap entry
- loaded by the native Doorstop transport beside `Subnautica.exe`
- validates the exact supported game build before runtime init
- loads the ranked runtime from the `Runtime` folder

`SubnauticaSpeedrunningRanked.Runtime.dll`

- the actual ranked runtime
- owns config, logs, module discovery, and future patching / networking
- writes runtime crash reports and environment diagnostics

## Why this approach

Subnautica September 2018 is a Unity Mono build. The cleanest practical path is to keep the native transport thin and make the real loader fully ours.

That gives us both:

- normal `Subnautica.exe` launches can still load the ranked runtime once installed
- `Launch Ranked.exe` can act as the safe setup and launch wrapper

## Deployment shape beside the game

```text
Subnautica2018\
  Subnautica.exe
  Launch Ranked.exe
  Launch Ranked.dll
  Launch Ranked.deps.json
  Launch Ranked.runtimeconfig.json
  winhttp.dll
  .doorstop_version
  doorstop_config.ini
  SubnauticaSpeedrunningRanked\
    Launch Ranked.exe
    Bootstrap\
      SubnauticaSpeedrunningRanked.Bootstrap.dll
    Runtime\
      SubnauticaSpeedrunningRanked.Runtime.dll
    Config\
    Logs\
      CrashReports\
    Modules\
    Cache\
    Data\
```

## Supported game build

The launcher and bootstrap are locked to the clean official September 2018 speedrun build:

- `FolderName=Subnautica2018`
- `DisplayName=2018`
- `OriginalDownload=Subnautica_Sep2018`
- `Modded=False`
- build `247`
- build time `9/29/2018 4:27:46 PM`
- `Subnautica.exe` file version `5.6.2.0`

If the install does not match, `Launch Ranked.exe` will stop before launch and tell the user to install the correct version. Direct `Subnautica.exe` launches are also blocked by the bootstrap validation once the ranked loader is installed.

## Scripts

- `build.ps1`
  Builds the solution in Release mode.

- `publish-to-game.ps1`
  Publishes the launcher/runtime into a game folder and can copy the native Doorstop transport from another install.

- `..\Build-ReleasePackage.ps1`
  Builds a public release folder and zip that can be extracted straight into the game root.

## Source layout

- `src\SubnauticaSpeedrunningRanked.Runtime\Core`
  Loader, validation, diagnostics, and runtime bootstrapping.
- `src\SubnauticaSpeedrunningRanked.Runtime\Server`
  Future match transport and server/API call code.
- `src\SubnauticaSpeedrunningRanked.Runtime\Seeds`
  Seed definitions, deterministic rolls, and spawn manipulation.
- `src\SubnauticaSpeedrunningRanked.Runtime\UI`
  Main menu changes and persistent overlays.
- `src\SubnauticaSpeedrunningRanked.Runtime\Speedrun`
  Timer logic, split detection, run state tracking, and creative-only helpers.

## Current scope

This first pass focuses on the loader foundation, not ranked gameplay features yet.

Next major steps after this foundation are:

- add Harmony-based patch orchestration under our own runtime
- add a signed module manifest format
- add update and self-check tooling
- add API client/auth/session plumbing for ranked services
