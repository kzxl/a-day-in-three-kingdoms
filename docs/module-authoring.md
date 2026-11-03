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

All cash goes through `context.Ledger` (or an `IWallet` capability). All time comes from
`context.Clock`. All ids come from `context.Ids`. Randomness comes from `context.Rng`.

## 6. Register screens

A screen is a read model — no WPF types:

```csharp
internal sealed class FamilyScreen : IScreenContribution
{
    public string ScreenId => "lineage.family";
    public string Title => "Gia Tộc";
    public int Order => 40;

    public ScreenModel Render() => new(
        ScreenId, Title, "Thế hệ 1",
        [new ScreenSection("Thành viên", ["Tên", "Vai trò"],
            _family.Members.Select(member => new ScreenRow(
                [member.NameVi, member.Role],
                [new ScreenCommand($"marry:{member.Id}", "Cưới")])).ToList())],
        []);
}
```

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
