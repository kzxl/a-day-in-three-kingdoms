# SimT

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
| `base.content` | base | 8 trade goods with base prices | `ct_item` |
| `base.world` | base | 3 citadels, roads with travel time, regional price factors | `wd_citadel`, `wd_route` |
| `base.player` | base | protagonist, per-city bag, wallet facade over the ledger | `pl_player`, `pl_bag` |
| `base.npc` | base | character stub that proves the boundary | `np_npc` |
| `dlc.market` | DLC | stall buy/sell, seasonal pricing, per-city warehouse with rent | `mk_*` |
| `dlc.caravan` | DLC | freight between cities, travel in days, arrival on the day it lands | `cv_*` |
| `dlc.professions` | DLC | 4 day-labour trades, practice tiers that raise yield | `pf_*` |
| `dlc.lineage` | DLC | marriage, children, one heir, monthly household upkeep | `ln_*` |
| `dlc.cultivation` | DLC | rented plots, seasonal sowing, harvest, monthly land rent | `fm_*` |
| `dlc.politics` | DLC | reputation from service, offices with gates, monthly stipend | `po_*` |

Every DLC declares its tables and migrations in its own `module.json`, reaches other modules
only through capabilities, and registers its own screens. Disabling one in
`assets/products/merchant-slice.json` removes its tabs and rules while its rows stay in the save.

## Current content of the slice

- 30-day months, 12-month years; day advance with `DayAdvanced` per day and one idempotent
  `MonthClosed` per boundary.
- Trade goods, regional and seasonal pricing, 60% buy-back, and a price that reacts to supply:
  a drained shelf costs up to 40% more, a glut is up to 30% cheaper, and stalls restock 60% of
  their gap every month with a hard buy-back ceiling.
- Warehouses with capacity, a real monthly rent cashflow, and spoilage: perishables decay daily
  (one unit per shelf-life day) and are gone once their age reaches the shelf life; durables
  never spoil.
- Freight: goods leave the current city, arrive after the road's duration, and lose part of
  their load on the road in proportion to transit time.
- Day labour: each batch costs its tool fee and days; practice raises the tier and the yield.
- Household: dowry, children, a single heir enforced by a unique index, monthly upkeep.
- Land: rent per plot, growth and yield set by season, soil that drops 25 per harvest and
  recovers 5 per month, fertiliser at 30 Quan, rice or tea per plot, and rent charged every
  month regardless.
- Court: service earns reputation, reputation unlocks an office, the office pays a stipend.
- Every cash movement posted by one ledger writer; spending draws on liquid cash, not the vault.
- Multiple independent campaigns, each one SQLite file, with duplicate/delete.
- Eight screens contributed by DLCs plus a kernel ledger view, rendered by a generic host.

## Verification snapshot

| Gate | Count | Command |
| --- | --- | --- |
| Architecture rules | 33 | `dotnet test tests/SimT.Architecture.Tests` |
| Kernel primitives, contracts and hardening | 49 | `dotnet test tests/SimT.Core.Tests` |
| Market slice + pricing budget | 26 | `dotnet test tests/SimT.Modules.Market.Tests` |
| DLC behaviour (6 DLCs) | 60 | `dotnet test tests/SimT.Modules.Dlc.Tests` |
| Golden ledger hashes | 2 | `SimT.Cli golden …` |
| Invariants at end of every replay | 26 | printed by `SimT.Cli` |
| WPF render smoke | 1 | `SimT.Host.Wpf.exe --smoke` |

## Documents

- [docs/architecture.md](docs/architecture.md) — kernel services, boundaries, save model.
- [docs/module-authoring.md](docs/module-authoring.md) — step-by-step DLC recipe.
- [docs/decisions.md](docs/decisions.md) — the choices and the rejected alternatives.
