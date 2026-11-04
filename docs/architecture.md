# Architecture

## 1. One process, one campaign, one file

```
%APPDATA%\SimT\saves\
  index.json                  list of campaigns (id, name, game date, last played)
  <uuid>\
    campaign.db              one SQLite database = one independent campaign
    campaign.lock            exclusive handle while the campaign is open
    backups\campaign-*.db    snapshot taken before a pending migration (last 5 kept)
```

There is no registry database, no lease and no heartbeat. A single local user opening a single
file does not need distributed coordination; an exclusive lock file covers the one case that
matters — the same campaign opened twice — and reports it at open time instead of failing
half-way through a turn. Disabling a DLC leaves its tables and rows in `campaign.db` untouched.

## 2. Kernel services (K1–K9)

| # | Contract | Implementation | Rule |
| --- | --- | --- | --- |
| K1 | `IDbSession` | `SqliteSession` | Modules never open a connection; transaction boundaries stay visible |
| K2 | `IGameClock`, `GameDate`, `IActionTime` | `SimClock`, `ActionTime`, `ActionCostPolicy` | No wall clock in domain code; every action spends days through the cost table; `DayAdvanced` per day and one `MonthClosed` per boundary recorded in `core_period_log` |
| K3 | `ILedger` | `SqliteLedger` | Single writer; balance = liquid + vault; amounts are positive integers |
| K4 | `IIdGenerator`, `IRng` | `SequenceIdGenerator`, `SplitMix64Rng` | Deterministic ids and rolls; RNG state is persisted in the save |
| K5 | `ModuleManifest`, `ICapabilityBroker` | `ModuleLoader`, `ModuleGraph`, `CapabilityBroker` | Deterministic topological order; duplicate ownership is a hard error |
| K6 | `IEventPublisher` | `EventPublisher` | Explicit subscription through `ModuleContext`, no discovery |
| K7 | `IUiRegistry` | `UiRegistry` | Screens are read models; the host renders them generically |
| K8 | `GameConstants`, `ModuleConfig` | `GameConstantsLoader`, `ModuleLoader` | JSON is the single source of truth; module settings must be flat scalars and a bad value fails at boot |
| K9 | `ILog`, `IInvariant` | `TextLog`, `InvariantRunner` | Module-scoped logging; kernel and modules register consistency rules that `RunInvariants()` and every CLI replay execute |

`GameDate` fixes a 30-day month and a 12-month year. `assets/config/game-constants.json` holds
the same numbers and an architecture test fails when the two drift.

## 3. Boot sequence

1. Open `campaign.db`, apply kernel migrations (`core_*` tables).
2. Load constants and the product manifest (seed, start location, starting cash, enabled modules).
3. Discover `Modules/SimT.Modules.*.dll`, read each embedded `module.json`.
4. Validate the composition: unknown module, missing dependency, dependency cycle, duplicate id,
   API version mismatch, table claimed twice, capability provided twice — all fatal.
5. Start modules in deterministic dependency order: apply that module's migrations from its own
   embedded resources, build its `ModuleContext`, run `ISimModule.Start`.
6. Persist `core_module_state` so a save records which modules were active.

Steps 2–5 are the only place where reflection is used, and only to instantiate one entry point
per module.

## 4. Capabilities instead of references

A module may not reference another module's assembly. It asks the broker for a contract
interface declared in `SimT.Contracts`:

```csharp
var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
var map    = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
```

The manifest declares what a module provides and consumes, so a missing provider fails at boot
with a named capability, not at runtime with a null reference.

## 5. Data ownership

Every table has exactly one owner and a prefix:

| Module | Prefix | Tables |
| --- | --- | --- |
| kernel | `core_` | ledger, state, sequence, module state, migration log, period log |
| base.content | `ct_` | item catalog |
| base.world | `wd_` | citadels, routes |
| base.player | `pl_` | protagonist, per-city bag |
| base.npc | `np_` | character stub |
| dlc.market | `mk_` | stall listings, warehouses, warehouse stock |
| dlc.caravan | `cv_` | freight runs, cargo in transit |
| dlc.professions | `pf_` | trade definitions, player practice |
| dlc.lineage | `ln_` | spouse, children and the designated heir |
| dlc.cultivation | `fm_` | plots, crops and the live-crop guard |
| dlc.politics | `po_` | offices and the player's court state |

Cash is not duplicated in `pl_player`: the wallet is a read of the kernel ledger.

The gate checks that a module's source never names a table it does not own, that declared tables
appear in that module's own migrations, and that no `CREATE TABLE` exists outside a migration.

## 6. UI without coupling

A screen is a read model, not markup:

```csharp
ScreenModel(ScreenId, Title, StatusLine, Sections, Commands, Metrics, Alerts)
ScreenMetric(Label, Value, Hint, Tone)          // headline strip
ScreenAlert(Message, Level, ScreenId)           // attention feed + rail badge
ScreenSection(Heading, Columns, Rows)
ScreenRow(Cells, Commands, Title, Details)      // inspector content
ScreenCommand(Id, Label, Kind, RequiresQuantity, Preview)
```

The host renders metrics as tiles, alerts as a feed, sections as cards, the selected row in an
inspector, and quantity commands with a live preview the module computes. Command ids are opaque
strings (`buy:3`, `buy:3:10`, `rent`), dispatched to the owning module's handler. Consequences:

- A new DLC adds a tab, its own headline numbers and its own warnings with zero host changes.
- No WPF type crosses the module boundary, so screens are testable headlessly.
- The host cannot implement game rules: every number, warning and cost preview comes from the
  module, which is why the price preview and the actual purchase cannot disagree.

## 7. Money and performance

- Money is a `long` of whole Quan, stored as `INTEGER`. No `decimal` or `double` in the pricing
  path, so rounding cannot drift between runs or platforms.
- `MarketPricing` scales by integer permille (region × season) and rounds once.
- `DataGrid` virtualization is enabled; table models are rebuilt per refresh, not per cell.
- The kernel rejects non-positive ledger amounts and direction values other than ±1 at both the
  contract and the SQLite trigger level.

## 7a. Time is the currency of actions

Every meaningful action costs whole days, looked up in `assets/config/action-costs.json`
(`trade.buy` 2, `caravan.send` 1, `farm.harvest` 2, …), with a default of one day for any code the
table does not list. A module calls `context.ActionTime.Charge(ActionCodes.X)` **inside its own
transaction**:

- the charge moves the calendar, so spoilage, arrivals and month close happen as a consequence;
- if the action then fails, the transaction rolls the days back — a failed action costs nothing;
- the charge publishes `ActionTimeCharged`, which is what the post-advance report reads.

Screens show `ScreenCommand.Cost` ("2 ngày") so the player sees the price before committing. Time
is not spent on bookkeeping actions (`market.warehouse_move`, `caravan.travel`, `lineage.beget`,
`politics.serve`) because their own duration already covers it.

## 8. Economy depth

The economy is not a set of fixed prices. Four curves decide what a trade is worth and what it
costs to hold goods:

| Curve | Rule | Where |
| --- | --- | --- |
| Supply | price multiplier 700–1400 permille as stock moves from 2× baseline to empty | `MarketPricing.ScarcityPermille` |
| Restock | 60% of the gap back to baseline each month; buy-back capped at 2× baseline | `MarketService.RestockMonthly` |
| Spoilage | 1/shelf-life of the quantity per day, sub-unit carried forward, whole row gone at shelf life | `MarketService.SpoilOneDay` |
| Transit loss | `quantity × transitDays / shelfLife`, rounded down, at least one unit survives | `CaravanService.Spoilage` |
| Soil | yield × (600 + soil×4)/1000, −25 per harvest, +5 per month, +30 for 30 Quan | `CropCalendar` |

Prices are also sampled once per day per settlement and item (`mk_price_sample`, sixty-day
window), which is enough for the screen to show a trend as text: the game is read, not watched.

All of them are integer functions of state that the save already holds. That is what keeps the
golden hash meaningful: two runs of the same script produce the same prices, the same spoil and
the same ledger.

## 9. Kernel safety net

The kernel assumes a module can be wrong and puts the last line of defence behind it:

| Risk | Guard | Where |
| --- | --- | --- |
| A migration leaves the save unreadable | snapshot before the first pending migration, keep 5 | `DatabaseBackup`, `SimKernel.BackupIfMigrationsPending` |
| A crash after a committed roll | RNG position written inside the same transaction as the day | `SimKernel.AdvanceDays` |
| A module spends money it does not have | `ledger.overdraft` refusal at the writer | `SqliteLedger.PostTo` |
| A retried command charges twice | optional idempotency key; the same key never writes a second line | `LedgerEntry.IdempotencyKey` |
| Two windows on one campaign | exclusive `<campaign>.lock` held for the kernel's lifetime, `save.locked` with the path | `CampaignLock` |
| An absurd calendar value | validated at load: 30-day month, 12-month year, 1–12 phases, non-negative fatigue | `GameConstantsLoader.Validate` |
| A driver error reaches the UI | `SqliteException` translated to `SimException("db.sqlite")` | `SqliteSession` |
| A save from a newer build | `save.future_version` refusal | `SimKernel.EnsureSchemaVersion` |
| Cash that cannot be attributed | every line carries the owning module | `JournalCodes.ModuleOf` |
| A DLC hard-coding a number | flat `config` block in `module.json`, typed read, bad value fails at boot | `ModuleConfig` |

Reports read through `ILedger.Query(from, to, journalCode?, moduleId?, limit)`, so "what did the
market cost me in month two" is one query, not a scan of the whole ledger.

## 10. Invariants and who enforces them

| Invariant | Enforced by |
| --- | --- |
| Contracts has no references | `Contracts_has_no_solution_references` |
| Core references Contracts only | `Core_references_contracts_only` |
| Modules reference Contracts only | `Module_references_contracts_only` |
| No business noun in Core | `Core_has_no_business_nouns` |
| One ledger writer | `Ledger_has_a_single_writer` |
| Tables have one owner | `Table_ownership_is_unique_across_modules` |
| Modules read only their own tables | `Modules_do_not_read_foreign_tables` |
| No DDL outside migrations | `No_create_table_outside_migrations` |
| No wall clock in modules | `Module_code_never_calls_wall_clock` |
| Calendar matches shared JSON | `Calendar_constants_match_the_shared_json` |
| Product manifest is complete | `Product_manifest_enables_every_module_and_satisfies_dependencies` |
| Consumers have providers | `Capabilities_consumed_are_provided_somewhere` |
| Host knows no module | `Host_source_never_mentions_module_types` |
| Same seed, same ledger | `Same_seed_and_same_actions_produce_the_same_ledger` |
| One month close per boundary | `Clock_publishes_each_boundary_only_once_even_if_the_date_is_rewound` |
| Pricing path allocates nothing | `Quote_allocates_nothing_over_ten_thousand_calls` |
| A newer save is refused | `Kernel_rejects_a_save_written_by_a_newer_schema` |
