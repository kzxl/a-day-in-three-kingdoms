# A Day in the Three Kingdoms (Một Ngày Ở Tam Quốc · SimT)

[![Build & Test](https://img.shields.io/badge/Tests-410%2B%20Passed-emerald.svg)](tests/)
[![Architecture](https://img.shields.io/badge/Architecture-Core%20%2B%20DLC%20Modular%20Monolith-blueviolet.svg)](docs/module-authoring.md)
[![Platform](https://img.shields.io/badge/Platform-.NET%208%20%7C%20WPF%20%7C%20SQLite%20WAL-blue.svg)](src/SimT.Host.Wpf/)
[![License](https://img.shields.io/badge/License-MIT-gray.svg)](LICENSE)

A single-player, offline, text-based merchant simulation for Windows, built as a **Core + DLC
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
| `SimT.Core` | Kernel: database, clock, ledger, ids, RNG, events, config, module runtime | Any business noun (market, warehouse, citadel…) |
| `SimT.Modules.Base.*` | Always-installed game pillars: content, world, player, npc | References to other modules |
| `SimT.Modules.Dlc.*` | Optional features that own their tables and screens | References to base modules' assemblies |
| `SimT.Host.Wpf` | Shell, save picker, generic screen renderer | Knowledge of any module type |
| `SimT.Cli` | Golden replay, balance simulation, manifest lint | Knowledge of any module type |

Dependency direction is checked, not asserted — see `tests/SimT.Architecture.Tests`.

## Adding a feature later (the point of the design)

A new feature is a new DLC: create a project, add `module.json`, add `migrations/*.sql`, declare
owned tables, expose capability interfaces, register screens, then add the module id to
`assets/products/*.json`. Core and the WPF shell are not edited. Removing the id disables the
feature while the save keeps its data intact.

Full recipe: [docs/module-authoring.md](docs/module-authoring.md).

## Build and run

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

## Modules shipped today

| Module | Kind | Adds | Owns |
| --- | --- | --- | --- |
| `base.content` | base | 24 trade goods, Item Encyclopedia Hub with deep lore records, category metadata & illustrations | `ct_item` |
| `base.world` | base | 20 citadels, 133 urban sublocations, 24 bidirectional trade routes, CityStroll interactive encounters | `wd_citadel`, `wd_route` |
| `base.player` | base | protagonist, per-city bag, wallet facade over ledger, two-way warehouse inventory exchange | `pl_player`, `pl_bag` |
| `base.npc` | base | 300 historical & procedural characters across 20 citadels | `np_npc` |
| `dlc.market` | DLC | stall buy/sell, seasonal pricing, per-city warehouse with rent and two-way storage transfer | `mk_*` |
| `dlc.caravan` | DLC | freight between cities, travel in days, arrival on the day it lands | `cv_*` |
| `dlc.professions` | DLC | 4 day-labour trades, practice tiers that raise yield | `pf_*` |
| `dlc.lineage` | DLC | marriage, children, one heir, monthly household upkeep | `ln_*` |
| `dlc.cultivation` | DLC | rented plots, seasonal sowing, harvest, monthly land rent | `fm_*` |
| `dlc.politics` | DLC | reputation from service, offices with gates, monthly stipend | `po_*` |
| `dlc.social` | DLC | 2-axis social network, gifting, sworn brotherhood, tiered retainers | `kz_*` |
| `dlc.military` | DLC | tactical martial duel, bandit strongholds, garrison arena | `mi_*` |
| `dlc.society` | DLC | autonomous 300 NPC lifecycle, monthly dynamic progression | `so_*` |
| `dlc.dialogue` | DLC | 2.5D character & role dialogue lines, relationship tiers, talk choices | `dg_*` |

Every DLC declares its tables and migrations in its own `module.json`, reaches other modules
only through capabilities, and registers its own screens. Disabling one in
`assets/products/merchant-slice.json` removes its tabs and rules while its rows stay in the save.

## Current content of the slice

- **Expanded Three Kingdoms World Map**: 20 historical citadels across North, Central, and South China (Lạc Dương, Trường An, Hứa Xương, Thành Đô, Kiến Nghiệp, Nghiệp Thành, Tương Dương...), 133 distinct urban sublocations (cung điện, đại thương thị, ngự lâm doanh, bến cảng, cổ đạo...), and 24 bidirectional trading routes.
- **Interactive City Stroll (`world.stroll`)**:
  - Rebalanced encounter distribution: 60% peaceful urban walk, 15% wandering merchant, 12% general/scholar encounter, 7% righteous deed, 4% teahouse rumor, 2% epiphany.
  - **Wandering Merchant Barter**: Browse 3 discounted goods; choose to purchase 1 item or politely decline.
  - **Interactive General / Scholar Dialogue**: 4 interactive paths evaluated against character attributes (`INT`, `WAR`, `LDR`):
    - 💬 *Đàm Luận Thế Cuộc* (Strategy Discourse): Challenge intellect and politics to gain intimacy and INT EXP.
    - ⚔️ *Thỉnh Giáo Võ Nghệ* (Martial Instruction): Spar with the general to hone combat skills and earn WAR EXP.
    - 🍶 *Mời Rượu Giao Hảo* (Wine Banquet): Spend 30 Quan for guaranteed friendship and high stress relief.
    - 👋 *Lễ Độ Cáo Từ* (Courteous Leave): Depart peacefully without commitment.
- **Two-Way Warehouse & Bag Exchange**: Move items seamlessly between personal bag and citadel warehouse via `ItemDetailModalWindow`, `NpcDetailWindow`, and `ItemHubScreen`.
- **Item Encyclopedia & Inspection**: Click on any item across inventory and warehouse to view ASCII art illustrations, deep lore descriptions, sourcing origins, and item statistics.
- **ROTK Segmented Domain Navigation**: Categorized header domain tabs (Nội Chính, Thương Mại, Võ Nghiệp, Quan Hệ, Bản Đồ...) providing clean, authentic strategy RPG navigation.
- **Economic & Market Simulation**:
  - 30-day months, 12-month years; day advance with `DayAdvanced` per day and one idempotent `MonthClosed` per boundary.
  - 24 trade goods with regional and seasonal pricing, 60% buy-back, and a price that reacts to supply.
  - Warehouses with capacity, monthly rent cashflow, and daily spoilage for perishables.
  - Freight: goods leave the current city, arrive after road duration, and face transit risk.
- **Career, Martial & Social Systems**:
  - Day labour trades with practice tiers and yield upgrades.
  - Lineage: marriage, children, designated heir, and monthly household upkeep.
  - Cultivation: rented farmland plots, seasonal crops, soil depletion and fertiliser management.
  - Politics: official appointments with reputation gates and stipends.
  - Arena duels, bandit stronghold suppression, and living autonomous society of 300 NPCs.

## Verification snapshot

| Gate | Count | Command |
| --- | --- | --- |
| Architecture rules | 41 | `dotnet test tests/SimT.Architecture.Tests` |
| Kernel primitives, contracts and hardening | 96 | `dotnet test tests/SimT.Core.Tests` |
| Market slice, pricing budget, price history | 41 | `dotnet test tests/SimT.Modules.Market.Tests` |
| DLC behaviour (9 DLCs) | 225 | `dotnet test tests/SimT.Modules.Dlc.Tests` |
| Total test suite | 403 | `dotnet test SimT.sln` |
| Golden ledger hashes | 2 | `SimT.Cli golden …` |
| Invariants at end of every replay | 26 | printed by `SimT.Cli` |
| WPF render smoke | 1 | `SimT.Host.Wpf.exe --smoke` |

## Documents

- [docs/architecture.md](docs/architecture.md) — kernel services, boundaries, save model.
- [docs/module-authoring.md](docs/module-authoring.md) — step-by-step DLC recipe.
- [docs/decisions.md](docs/decisions.md) — the choices and the rejected alternatives.
