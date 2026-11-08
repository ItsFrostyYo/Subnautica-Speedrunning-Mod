# Subnautica Speedrunning Mod

Current build: `Beta-0.11.3`

This is a Custom Launcher and Mod for Subnautica Septmember 30th 2018 Version of the game, which is the most prominent Speedrunning Version.

## What The Mod Does

This Mod is specifically for Speedrunning, and will give you the ability to use BetterRNG Which is a Increased RNG Preset which will allow you to complete runs easier without needing to rely on rolling good RNG, and is mainly used to Practice and for the Unofficial Category Extentions Leaderboard for Any% BetterRNG, and in the future there will be a Ranked Feature where you can Player Singleplayer or Multiplayer to Compete against others, rolling different Seeds to Increase the RNG Enough to be consistent, and Practice Saves which can help you Improve your Skill at Speedrunning Subnautica, you can also easily load your Normal Save Files and Vanilla Subnautica Save File Creation.

Survival Race Practice and Survival 1v1 use the Set Seed Ruleset 7 engine. Singleplayer chooses a numeric seed from the bundled filtered 1,000-seed race pool; in 1v1 the server chooses one numeric seed and sends the same value to both players. Ruleset 7 generates the lifepod position and seeded world directly, so the race path does not stage batch files, reroll Clip C coordinates, force a separate lifepod position, or apply the old Survival race profile rewrites. BetterRNG, Creative races, practice saves, and Vanilla remain separate modes.

### Updates

Practice settings now use the game's own tab toggle, layout and choice controls. Hotbar and health images fit inside the native value area; Day/Night uses the original choice text. Native hover, selection, arrow sizes and keyboard/controller navigation are retained.

Subnautica.exe launches hand off once to Launch Mod.exe, so direct and Steam launches use the same validation and update flow as Launch Mod.cmd. Steam must launch the game folder containing this mod package. The launcher clears inherited Doorstop startup flags before starting the game, and the bootstrap consumes a one-use launch marker to prevent relaunch loops.

General settings now include Optimizations > Improve Preload Base, a native Off/On choice that defaults to On and persists between launches. On reuses loaded base-building prefab assets between attempts in the same game session. Off uses the game's original base-piece loading path. Only prefab assets are retained; each world and its base registrations are fresh. The filtered 1,000-seed pool is bundled with this build.

The updater prompts when a newer published package is available. The Beta-0.11.3 package was tested with the actual Beta-0.11.1 launcher/updater, including preservation of user data and restoration after a blocked update write.
