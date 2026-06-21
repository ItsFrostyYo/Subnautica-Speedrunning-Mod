# Repository Workflow

This repo is intended to stay clean for public releases while still letting development move quickly.

## Branch model

- `development`
  Active development branch. New gameplay work, UI changes, timer work, seed work, and server-call scaffolding land here first.
- `main`
  Stable release branch. Only tested changes should be merged here.

## Recommended maintainer flow

1. Work in `development`.
2. Build and test locally with:
   `.\Launcher\publish-to-game.ps1 -BuildFirst`
3. When the branch is stable, merge `development` into `main`.
4. Build the public package from `main` with:
   `.\Build-ReleasePackage.ps1 -BuildFirst`
5. Upload the generated zip from `release\` to the GitHub release page.

## Player install/update flow

1. Download the latest release zip from GitHub.
2. Fully close the game.
3. Extract the zip into the `Subnautica2018` game folder.
4. Launch with `Launch Ranked.exe`.
5. To update, replace the existing ranked files with the new release contents.

## Repo scope

This repo should contain only the public mod project:

- launcher
- bootstrap
- runtime
- public docs
- packaging scripts
- GitHub workflow files

Keep private research, external experiments, and unrelated tools outside this repo.
