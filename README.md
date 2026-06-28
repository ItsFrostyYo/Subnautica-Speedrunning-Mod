# Subnautica Speedrunning Mod

Current public beta: `Beta-0.3.3`

This is a custom ranked client for the official September 2018 Subnautica speedrun build. It is not a generic mod pack and it is not built around BepInEx. The client is made specifically for the real September 2018 game files, with its own launcher, loader, runtime, UI layer, seed system, timer system, and future ranked support.

## What The Client Does

When you launch the mod, the client:

1. Verifies that the game folder matches the supported September 2018 build.
2. Enables the ranked bootstrap/runtime for that launch.
3. Applies ranked UI changes and shared runtime systems.
4. Starts Subnautica normally with the ranked client loaded into it.

The ranked files live inside a single folder:

- `SubnauticaSpeedrunningMod`

The game root also contains:

- `winhttp.dll`
- `.doorstop_version`
- `doorstop_config.ini`

The real launcher is:

- `SubnauticaSpeedrunningMod\Launch Mod.exe`

## Supported Game Version

This client is for the clean official Subnautica September 2018 speedrun build.

The launcher checks standard game files that real installs already have, such as:

- `Subnautica.exe`
- `SubnauticaMonitor.exe`
- `Subnautica_Data\Managed\Assembly-CSharp.dll`
- `Subnautica_Data\Managed\Assembly-CSharp-firstpass.dll`
- `__buildnumber.txt`
- `__buildtime.txt`

If the wrong game build is detected, the mod will stop and tell the player to restore the correct September 2018 version instead of launching on a mismatched install. The folder can be named whatever you want as long as the actual game files match.

## Install Layout

Install the release contents into the same folder that contains `Subnautica.exe` so the final layout looks like this:

```text
Your Subnautica Game Folder
|- Subnautica.exe
|- winhttp.dll
|- .doorstop_version
|- doorstop_config.ini
\- SubnauticaSpeedrunningMod
   |- Launch Mod.exe
   |- Bootstrap
   |- Runtime
   |- Updater
   |- Config
   |- Data
   |- Logs
   \- Modules
```

## Launch Flow

Players can launch with either:

- `Subnautica.exe`
- `SubnauticaSpeedrunningMod\Launch Mod.exe`

`Subnautica.exe` will still load the mod because Doorstop is installed into the game root. `Launch Mod.exe` is included for a direct client launch path with the same validation and update flow.

## Updates

The updater is built into the client.

On launch, the mod checks the public `main` branch `release/` folder for a newer package. If a newer version exists, the launcher can:

1. Prompt the player to update.
2. Close the launcher.
3. Download the release zip.
4. Replace the mod files.
5. Relaunch automatically.

This update flow reads from the repository release folder, not GitHub Releases.

## Logs And Crash Reports

The mod writes logs to:

- `SubnauticaSpeedrunningMod\Logs`

Important files:

- `launcher-YYYYMMDD.log`
- `runtime-YYYYMMDD.log`
- `bootstrap.log`
- `CrashReports\`

If launch fails, those logs are the first place to check.

## Current Focus

This beta already includes the custom launcher/runtime base, main menu UI work, persistent overlays, seed storage, and the early speedrun systems. Future versions will keep building on this with more survival seed control, more polished timing and split rules, and the later ranked/server pieces.
