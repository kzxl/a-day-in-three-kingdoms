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
dotnet run --project src/SimT.Cli -- golden samples/merchant-30d.json samples/merchant-30d.expected.txt
```

The hash covers every ledger line: date, journal code, direction, amount and running balance.
Same seed plus same action script must produce the same hash, so a rules refactor cannot change
the numbers silently. This is the mechanism that lets the slice be ported or extended safely.

## Current content of the slice

- 30-day months, 12-month years; day advance with automatic month close.
- 3 citadels with regional price factors, 8 trade goods.
- Buy/sell at the stall, seasonal and regional pricing, 60% buy-back.
- Per-city warehouses with capacity and a real monthly rent cashflow.
- Every cash movement posted by one ledger writer; balance = liquid + vault.
- Multiple independent campaigns, each one SQLite file, with duplicate/delete.
- 3 screens contributed by DLC (`market.stall`, `market.warehouse`) plus a kernel ledger view.

## Verification snapshot

| Gate | Count | Command |
| --- | --- | --- |
| Architecture rules | 23 | `dotnet test tests/SimT.Architecture.Tests` |
| Kernel primitives | 8 | `dotnet test tests/SimT.Core.Tests` |
| Market slice + pricing budget | 11 | `dotnet test tests/SimT.Modules.Market.Tests` |
| Golden ledger hash | 1 | `SimT.Cli golden …` |
| WPF render smoke | 1 | `SimT.Host.Wpf.exe --smoke` |

## Documents

- [docs/architecture.md](docs/architecture.md) — kernel services, boundaries, save model.
- [docs/module-authoring.md](docs/module-authoring.md) — step-by-step DLC recipe.
- [docs/decisions.md](docs/decisions.md) — the choices and the rejected alternatives.
