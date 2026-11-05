# Release

The desktop build is a **framework-dependent single-file** executable: one `SimT.Host.Wpf.exe`
plus the module DLLs in `Modules/` and the data files in `assets/`. It does not bundle the .NET
runtime, so the download stays a couple of megabytes; the target machine needs the
**.NET 8 Desktop Runtime (x64)**.

## One command

```bash
release.cmd
```

The script is the release contract and runs, in order:

1. `dotnet clean -c Release`
2. `dotnet restore`
3. `dotnet build -c Release -warnaserror` — a warning fails the release
4. `dotnet test -c Release --no-build` — the full 168-test gate
5. `dotnet publish … --self-contained false -p:PublishSingleFile=true`
6. `SimT.Host.Wpf.exe --smoke` — boots, creates a campaign, renders all tabs, exits 0
7. `Compress-Archive` into `dist/SimT-<version>-win-x64.zip`
8. prints the artifact size and the SHA-256 of the zip

## Artifact layout

```
SimT.Host.Wpf.exe        the game (single file, ~2.3 MB)
QUICKSTART.txt           what a player needs to know
Modules\                 base + DLC modules (10 DLLs in 0.1.0)
assets\config\           game-constants.json
assets\products\         merchant-slice.json (enabled modules, seed, starting cash)
assets\scenes\           WebView2 interactive scenes (HTML/CSS/JS, fonts, art, pixelfx)
```

Modules stay outside the single-file bundle on purpose: a DLC is an artefact, not a reference, so
a build can drop or add one without recompiling the host. `CopyModulesOnPublish` in the host
project copies them into the publish folder.

## Version single source of truth

`Directory.Build.props` holds `Version`, `AssemblyVersion`, `FileVersion` and
`InformationalVersion`. The informational version records the commit it was built from, which is
how a bug report maps back to the tree.

Bump all of them together, in the same commit as the change that warrants the release.

| Change | Bump |
| --- | --- |
| bug fix, no schema change | PATCH |
| new DLC, new save schema, new mechanics | MINOR |
| save file no longer readable by 0.x readers | MAJOR |

## Tag policy

```bash
git tag -a v0.1.0 -m "chore(release): v0.1.0 lite distribution"
```

Tags are forward-only: never move or delete a published tag. A defect found after a release gets
a fix commit and the next PATCH; the faulty artifact is quarantined, not rewritten.

## Size budget

| Tier | Expected | Investigate above |
| --- | --- | --- |
| WPF client, lite single-file | 2–6 MB exe | 8 MB |

0.2.x: 2.6 MB across 14 files, 1.4 MB zipped. If the exe crosses 8 MB, the usual cause is
`--self-contained true` slipping in, which bundles the whole desktop runtime.

## Verifying a downloaded build

```powershell
Get-FileHash SimT-0.1.0-win-x64.zip -Algorithm SHA256
```

Compare with the checksum published next to the artifact (also printed by `release.cmd`). Then
run `SimT.Host.Wpf.exe` — the title screen lists campaigns and shows the version in the header.

## Developer preview

The same binary can open the play screen directly, with a little seeded activity, for UI work:

```bash
SimT.Host.Wpf.exe --demo                          # play screen on a throwaway campaign
SimT.Host.Wpf.exe --demo --screen cultivation.farm # a specific screen
SimT.Host.Wpf.exe --demo --screen market.stall --row # with the first row selected (inspector)
```

Demo data lives in the temp folder and is replaced on every launch.

## What a release does not include

- No auto-update, no telemetry, no network access.
- Saves live in `%APPDATA%\SimT\saves` and are never touched by replacing the build.
- The save schema version guards downgrades: a save written by a newer build refuses to open with
  `save.future_version` instead of being silently misread.
