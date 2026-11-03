# Decisions

Short records of what was chosen, what was rejected and why.

## D1 — WPF over Godot

**Chosen:** WPF (`net8.0-windows`).

**Rejected:** Godot 4 with C#; WinForms; Avalonia; Blazor Hybrid.

The game renders text, tables and numbers — zero sprites, zero animation, zero physics. Godot's
strengths are unused, while its UI layer has no virtualized data grid, no two-way binding and no
form validation, so a management screen costs several times more than in XAML. Godot is also not
lighter: a C# export is typically 70–110 MB and starts with a render loop the game never needs.
WinForms is not lighter than WPF either — both sit on the same desktop runtime — and it cannot
express the existing CSS-derived design without owner drawing. Avalonia remains the choice if
Linux/macOS support becomes a requirement; the domain does not change.

**Revisit if:** the game gains a tile map, portraits and battle animation as first-class visuals.

## D2 — No dependency injection container

**Chosen:** explicit composition. Each module's `Start` builds its object graph with `new`;
`ModuleContext` carries the kernel services.

**Rejected:** `Microsoft.Extensions.DependencyInjection` or a reflection container.

Constructor injection with a small context is enough for a dozen services, keeps startup
allocation-free and reflection-free, and matches the legacy design review. The only reflection
in the system is loading one module entry point per DLL.

## D3 — No HTTP layer

**Chosen:** in-process calls and a UI contribution registry.

**Rejected:** local web server, REST endpoints, IPC.

The web build needed 206 endpoints and a session-lease layer to serve one player. A desktop
process renders read models directly. This removes controllers, DTO mapping and instance
arbitration entirely.

## D4 — One SQLite file per campaign

**Chosen:** `saves/<uuid>/campaign.db`, one file per save, listed in `saves/index.json`.

**Rejected:** a single database with a `campaign_id` column; a registry database with locks.

Every existing campaign is trivially copyable, backupable and deletable. There is no cross-save
join to get wrong and no lock protocol to debug. The legacy project's per-campaign file model was
right; what it did not need was the lease/heartbeat layer around it.

## D5 — Capabilities instead of references

**Chosen:** modules exchange interfaces declared in `SimT.Contracts` through a broker, and the
manifest declares provides/consumes.

**Rejected:** direct project references between modules, or a shared "common" module.

A shared common module becomes a dumping ground, and direct references make disabling a DLC
impossible. A missing capability fails at boot with the capability name.

## D6 — Money as integer Quan

**Chosen:** `long` whole Quan in code and `INTEGER` in SQLite; pricing uses integer permille.

**Rejected:** `decimal` or floating point.

Determinism: the same seed and actions must produce a byte-identical ledger hash, which makes the
golden test a real regression gate. Integer math also avoids SQLite's lack of a decimal type.

## D7 — Product manifest instead of build-time composition

**Chosen:** `assets/products/merchant-slice.json` lists enabled modules at runtime.

**Rejected:** `#if` symbols, separate builds per edition.

A DLC can be switched off without recompiling, and a save keeps the data of a disabled module, so
re-enabling it later restores the content instead of losing it.

## D8 — Architecture rules as tests, not as documentation

**Chosen:** `tests/SimT.Architecture.Tests` inspects compiled assemblies and source files.

**Rejected:** a written convention, a code-review checklist, an analyzer-only approach.

The web project had the same target architecture written down in an audit and it never became
true, because nothing failed when a rule was broken. Here a forbidden reference, a second ledger
writer, a foreign table read or a business noun in the kernel turns the build red.
