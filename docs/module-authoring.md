# Authoring a DLC

Everything below is additive. If a step requires editing `SimT.Core`, `SimT.Host.Wpf` or another
module, the design is being violated — stop and reconsider the boundary.

## 1. Create the project

```bash
dotnet new classlib -n SimT.Modules.Dlc.Lineage -o src/SimT.Modules.Dlc.Lineage -f net8.0
dotnet sln SimT.sln add src/SimT.Modules.Dlc.Lineage/SimT.Modules.Dlc.Lineage.csproj
```

`SimT.Modules.Dlc.Lineage.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>SimT.Modules.Dlc.Lineage</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\SimT.Contracts\SimT.Contracts.csproj" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="module.json">
      <LogicalName>module.json</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="migrations\*.sql" />
  </ItemGroup>
</Project>
```

Reference **Contracts only**. A module that needs a database session, a clock, a ledger or
another module's data gets them from `ModuleContext` — never from a project reference.

## 2. Declare the manifest

`module.json`:

```json
{
  "id": "dlc.lineage",
  "version": "0.1.0",
  "apiVersion": "1",
  "kind": "dlc",
  "requires": ["base.player"],
  "provides": ["lineage.family"],
  "consumes": ["player.state"],
  "owns": { "tables": ["ln_family", "ln_marriage"], "migrations": ["migrations"] },
  "screens": ["lineage.family"]
}
```

Rules the loader enforces at boot:

- `apiVersion` must equal `ModuleManifest.CurrentApiVersion`.
- every id in `requires` must be enabled in the product manifest.
- a table may be owned by exactly one module.
- a capability may be provided by exactly one module.
- the dependency graph must be acyclic.
- `config` must be a flat object of scalars; nested values are rejected.

## 2a. Settings belong in the manifest, not in code

Anything a balance designer might tune goes in `config`, with the code value as the default:

```json
"config": { "freightPerUnit": 8 }
```

```csharp
var freight = context.Config.GetLong("freightPerUnit", CaravanService.DefaultFreightPerUnit);
var required = context.Config.RequireLong("someMandatoryValue");   // throws at boot if absent
```

`RequireLong` fails with `module.config_missing` or `module.config_invalid`, so a typo in a
manifest is a startup error rather than a silent fallback.

## 3. Own your tables

`migrations/0300-dlc-lineage.sql` — prefix every table with your module's prefix, create only
your own tables, seed only your own rows:

```sql
CREATE TABLE IF NOT EXISTS ln_family (
    id      INTEGER PRIMARY KEY,
    name_vi TEXT NOT NULL
);
```

Migration ids are unique per module and applied in ordinal filename order. Never read another
module's table in a migration; seed from capabilities in `Start` instead (see
`MarketService.EnsureListings`).

## 4. Declare the capability surface in Contracts

Cross-module contracts live in `SimT.Contracts/Playing`, next to a capability id:

```csharp
public interface IFamily
{
    IReadOnlyList<FamilyMember> Members { get; }
}

public sealed record FamilyMember(long Id, string NameVi, string Role);
```

and

```csharp
public static class CapabilityIds
{
    public const string Family = "lineage.family";
}
```

## 5. Implement the module

```csharp
public sealed class LineageModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var family = new FamilyService(context.Db, player);

        context.Capabilities.Provide(CapabilityIds.Family, family);
        context.Ui.AddScreen(new FamilyScreen(family));
        context.Ui.AddCommandHandler(new FamilyCommandHandler(family));
        context.Events.Subscribe<MonthClosed>(closed => family.AdvanceMonth(closed.Date));
        context.Events.Subscribe<DayAdvanced>(day => family.OnDay(day.Date));

        foreach (var invariant in FamilyInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"lineage ready for player {player.PlayerId}");
    }
}
```

All cash goes through `context.Ledger` (or an `IWallet` capability). All ids come from
`context.Ids`. Randomness comes from `context.Rng`.

Time is not free: charge the action's days through the kernel and pass the same quote to the
button so the player sees it.

```csharp
_db.Transaction(() =>
{
    context.ActionTime.Charge(ActionCodes.LineageMarry);   // days come from action-costs.json
    _wallet.Charge(cost, JournalCodes.Dowry, "…");
    // …the rest of the action
});

new ScreenCommand("marry", "Cầu hôn", Cost: context.ActionTime.Quote(ActionCodes.LineageMarry).ToString())
```

Add a row to `assets/config/action-costs.json` for a new code; anything unlisted costs the
policy default (one day).

## 6. Register screens

A screen is a read model — no WPF types. Give it numbers worth reading and warnings worth acting
on; the shell renders both generically:

```csharp
public ScreenModel Render() => new(
    ScreenId, Title, "Thế hệ 1",
    [new ScreenSection("Thành viên", ["Tên", "Vai trò"],
        _family.Members.Select(member => new ScreenRow(
            [member.NameVi, member.Role],
            [new ScreenCommand($"marry:{member.Id}", "Cưới"),
             new ScreenCommand($"dowry:{member.Id}", "Sính lễ…", RequiresQuantity: true,
                 Preview: qty => $"{qty} lễ vật = {qty * 30} quan")],
            Title: member.NameVi,
            Details: [new ScreenDetail("Vai trò", member.Role)])).ToList())],
    [],
    [new ScreenMetric("Thành viên", _family.Members.Count.ToString(), "người"),
     new ScreenMetric("Phí tháng", _family.Upkeep.ToString(), "quan/tháng", MetricTone.Neutral)],
    [new ScreenAlert("Chưa lập người kế vị.", AlertLevel.Warning)]);
```

`Preview` is how a module keeps pricing rules: the shell shows the sentence, the module does the
maths, and the number the player approves is the number that gets charged.

Command ids are opaque to the host. Parse them in your `IScreenCommandHandler`.

## 7. Enable it

Add the id to `assets/products/merchant-slice.json`:

```json
"modules": ["base.content", "base.world", "base.player", "base.npc", "dlc.market", "dlc.lineage"]
```

Then add the project to the artefact lists of the two heads:

```xml
<ModuleProject Include="..\SimT.Modules.Dlc.Lineage\SimT.Modules.Dlc.Lineage.csproj" />
```

Both heads copy module DLLs into `Modules/` with `ReferenceOutputAssembly="false"`, so they can
load the module at runtime without referencing its types at compile time.

## 8. Prove the boundary

```bash
dotnet test tests/SimT.Architecture.Tests      # boundaries, ownership, manifest rules
dotnet test tests/SimT.Modules.Lineage.Tests   # behaviour
dotnet run --project src/SimT.Cli -- golden samples/<scenario>.json samples/<scenario>.expected.txt
```

Your scenario can drive your screens without the CLI knowing your module:

```json
{ "type": "command", "screen": "lineage.family", "id": "heir:3" }
```

Every replay prints `invariants ok (n rules)` and exits non-zero when a rule fails, so a new
invariant becomes a standing regression check the moment it is registered.

Add your assembly to `RepoLayout.ModuleAssemblies()` so the gate inspects it, plus one behaviour
test that boots the kernel with your module enabled. Test projects should use
`SimT.Testing.TestHost`, which filters the product manifest to the module artefacts present in
the test output — that way adding a DLC never breaks an unrelated test project.

## Checklist

- [ ] `module.json` embedded as the resource named exactly `module.json`
- [ ] tables prefixed and listed in `owns.tables`
- [ ] every declared table created in your own migrations
- [ ] capability interfaces in `SimT.Contracts`, ids in `CapabilityIds`
- [ ] no `DateTime.Now`, no `new Random()`, no direct ledger insert, no foreign table name
- [ ] screens pure data, commands parsed by your handler
- [ ] product manifest and both heads updated
- [ ] gate green, behaviour test added
