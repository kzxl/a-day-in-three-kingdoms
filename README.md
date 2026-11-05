# A Day in the Three Kingdoms (Một Ngày Ở Tam Quốc · SimT)

[![Build & Test](https://img.shields.io/badge/Tests-430%20Passed-emerald.svg)](tests/)
[![Architecture](https://img.shields.io/badge/Architecture-Core%20%2B%204%20DLC%20Pillars%20Monolith-blueviolet.svg)](docs/module-authoring.md)
[![Platform](https://img.shields.io/badge/Platform-.NET%208%20%7C%20WPF%20%7C%20SQLite%20WAL-blue.svg)](src/SimT.Host.Wpf/)
[![License](https://img.shields.io/badge/License-MIT-gray.svg)](LICENSE)

A single-player, offline, text-and-tactical merchant simulation for Windows, built as a **Core + DLC
modular monolith**. This repository is a deliberate restart of the `Simulation` web project:
same game genre, but the architecture that project only documented is enforced here from the
first commit by the compiler and by an architecture test suite.

## Why a new project

The web build reached 52k lines of TypeScript, 206 HTTP endpoints and a session-lease layer
(60 s leases, heartbeats, `423 SESSION_LOCKED`) to manage instances of a game that is played by
one person against one local database. For an offline single-player title that machinery is pure
overhead: no HTTP, no leases, no bundle rebuilds. A desktop process with one SQLite file per
campaign replaces all of it.

## What "Core + DLC" means here

| Layer | What belongs | What must never appear |
| --- | --- | --- |
| `SimT.Contracts` | Interfaces, DTOs, value objects, capability ids | Any project or package reference |
| `SimT.Core` | Kernel: database, clock, ledger, ids, RNG, events, config, module runtime, idempotent migrations | Any business noun (market, warehouse, citadel…) |
| `SimT.Modules.Base.*` | Always-installed game pillars: content, world, player, npc | References to other modules |
| `SimT.Modules.Dlc.*` | 4 Consolidated DLC Pillars: `Economy`, `Character`, `Power`, `Society` | References to base modules' assemblies |
| `SimT.Host.Wpf` | Shell, save picker, ROTK tactical HUD, Imperial Time pill, generic screen renderer | Knowledge of any module type |
| `SimT.Cli` | Golden replay, balance simulation, manifest lint | Knowledge of any module type |

Dependency direction is checked, not asserted — see `tests/SimT.Architecture.Tests`.

## Adding a feature later (the point of the design)

A new feature is a new DLC or an extension to an existing Pillar: create a project, add `module.json`, add `migrations/*.sql`, declare
owned tables, expose capability interfaces, register screens, then add the module id to
`assets/products/*.json`. Core and the WPF shell are not edited. Removing the id disables the
feature while the save keeps its data intact.

Full recipe: [docs/module-authoring.md](docs/module-authoring.md).

## Quick Start & Running the Game

To launch the desktop game directly from the root repository:

```cmd
run.cmd
```

Or build and run via the .NET CLI:

```bash
dotnet build SimT.sln
dotnet test SimT.sln
dotnet run --project src/SimT.Host.Wpf        # desktop game
dotnet run --project src/SimT.Cli -- info     # where modules and assets resolve
```

`verify.cmd` runs the whole gate: Release build, all tests, then the golden replay.

## Release

```bash
release.cmd
```

Builds, tests, publishes the framework-dependent single-file Windows build, smoke-runs it and
writes `dist/SimT-<version>-win-x64.zip` with a SHA-256. The player needs the .NET 8 Desktop
Runtime; save data lives in `%APPDATA%\SimT`. Details: [docs/release.md](docs/release.md).

## Golden replay

```bash
dotnet run --project src/SimT.Cli -- golden samples/merchant-30d.json  samples/merchant-30d.expected.txt
dotnet run --project src/SimT.Cli -- golden samples/merchant-full.json samples/merchant-full.expected.txt
```

The hash covers every ledger line: date, journal code, direction, amount and running balance.
Same seed plus same action script must produce the same hash, so a rules refactor cannot change
the numbers silently. Every replay also runs all registered invariants and exits non-zero if
any table contradicts another.

## Consolidated DLC Pillars & Base Modules

The gameplay systems are structured into cohesive, deep architectural modules:

| Module | Kind | Focus Domains | Owned Schema |
| --- | --- | --- | --- |
| `base.content` | Base | 24 trade goods, Item Encyclopedia Hub with deep lore records, category metadata & illustrations | `ct_item` |
| `base.world` | Base | 20 citadels, 133 urban sublocations, 24 bidirectional trade routes, CityStroll interactive encounters | `wd_citadel`, `wd_route` |
| `base.player` | Base | Protagonist attributes, per-city bag, wallet facade over ledger, two-way warehouse inventory exchange | `pl_player`, `pl_bag` |
| `base.npc` | Base | 300 historical & procedural characters across 20 citadels | `np_npc` |
| `dlc.economy` | Pillar | Market trading, seasonal prices, warehouses, caravan logistics, day-labour professions, and farmland cultivation | `mk_*`, `cv_*`, `pf_*`, `fm_*` |
| `dlc.character` | Pillar | NPC relationship tiers, dialogue trees, gift exchanges, sworn brotherhood, marriage, children, and lineage heir | `kz_*`, `dg_*`, `ln_*` |
| `dlc.power` | Pillar | Court official appointments, reputation ranks, monthly stipends, martial duels, bandit stronghold suppression | `po_*`, `mi_*` |
| `dlc.society` | Pillar | Autonomous 300-NPC simulation, rival merchants, karma consequences, dynamic life events, and chronicle logs | `so_*` |

### Backward Compatibility & Idempotent Migrations
Legacy saves containing historical records from individual micro-modules (`dlc.market`, `dlc.caravan`, etc.) are automatically normalized and mapped to the 4 Pillars via `SaveManager.NormalizeModules` and a global idempotent `MigrationRunner`.

## User Interface & Experience (UI/UX)

- **Top Header Imperial Time HUD**:
  - Integrated directly into the main player vitals bar at the top of the screen.
  - Displays calendar date, current season, turn number, and day-of-month progress (`📅 16/06/184 · Mùa Hạ · Lượt 2 · Ngày 16/30`).
  - Tactile mini-progress bar (1–30 days) and dynamic transition ticker (`+1 ngày`, `+7 ngày`) on day advances.
  - Replaces bulky multi-slot calendar grids with a clean, high-density strategy HUD.
- **Visual Vitals & ROTK Gauges**:
  - Real-time gauges for Stamina (Thể lực), Fatigue (Độ mệt), and Prestige Standing (Danh vọng).
- **Contextual Strategic Dock**:
  - Categorized left command sidebar (Sa Bàn 13 Châu, Thương Đạo, Thế Lực Triều Đình, Nhân Duyên Hào Kiệt, Bản Thân & Sổ Sách).
  - Clean separation between contextual urban exploration (`CitySceneView`) and strategic spreadsheet views.
- **Interactive City Stroll (`world.stroll`)**:
  - Historical encounters: Wandering merchants, scholars, generals, banquet invitations, and martial spars evaluated against `INT`, `WAR`, and `LDR`.

## Verification snapshot

| Gate | Count | Command |
| --- | --- | --- |
| Architecture rules | 31 | `dotnet test tests/SimT.Architecture.Tests` |
| Kernel primitives, contracts and hardening | 100 | `dotnet test tests/SimT.Core.Tests` |
| Market slice, pricing budget, price history | 42 | `dotnet test tests/SimT.Modules.Market.Tests` |
| Consolidated DLC Pillars behavior | 257 | `dotnet test tests/SimT.Modules.Dlc.Tests` |
| **Total test suite** | **430** | `dotnet test SimT.sln` |
| Golden ledger hashes | 2 | `SimT.Cli golden …` |
| Invariants at end of every replay | 26 | printed by `SimT.Cli` |
| WPF render smoke | 1 | `SimT.Host.Wpf.exe --smoke` |

## Documents

- [docs/architecture.md](docs/architecture.md) — kernel services, boundaries, save model.
- [docs/module-authoring.md](docs/module-authoring.md) — step-by-step DLC recipe.
- [docs/decisions.md](docs/decisions.md) — the choices and the rejected alternatives.
