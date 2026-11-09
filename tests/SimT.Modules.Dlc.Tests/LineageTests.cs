using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Modules.Dlc.Character;
using SimT.Testing;

namespace SimT.Modules.Dlc.Tests;

/// <summary>Household: dowry, children, one heir, and upkeep that actually gets charged.</summary>
public sealed class LineageTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Marriage_records_a_spouse_and_charges_the_dowry()
    {
        using var kernel = _host.Boot();
        var before = kernel.Ledger.Balance;

        var message = Command(kernel, "marry");

        Assert.Contains("Thành hôn", message);
        Assert.True(House(kernel).IsMarried);
        Assert.NotNull(House(kernel).SpouseName);
        Assert.Equal(before - LineageService.DowryCost, kernel.Ledger.Balance);
        Assert.Contains(kernel.Ledger.Recent(20), line => line.JournalCode == JournalCodes.Dowry);
    }

    [Fact]
    public void A_second_marriage_is_refused()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");

        var message = Command(kernel, "marry");

        Assert.Contains("Đã có chính thất", message);
        Assert.Equal(1, kernel.Db.ScalarLong("SELECT COUNT(*) FROM ln_spouse"));
    }

    [Fact]
    public void Children_require_a_marriage()
    {
        using var kernel = _host.Boot();

        var message = Command(kernel, "beget");

        Assert.Contains("Chưa có chính thất", message);
        Assert.Equal(0, House(kernel).ChildCount);
    }

    [Fact]
    public void Children_are_born_after_marriage_and_cost_a_fee()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        var before = kernel.Ledger.Balance;

        var message = Command(kernel, "beget");

        Assert.Contains("chào đời", message);
        Assert.Equal(1, House(kernel).ChildCount);
        Assert.Equal(before - LineageService.ChildCost, kernel.Ledger.Balance);
    }

    [Fact]
    public void Exactly_one_child_can_be_heir()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");
        Command(kernel, "beget");

        var children = kernel.Db.Query("SELECT id FROM ln_child ORDER BY id", row => row.GetInt64(0));
        Command(kernel, $"heir:{children[0]}");
        var firstHeir = House(kernel).HeirName;
        Command(kernel, $"heir:{children[1]}");

        Assert.NotNull(firstHeir);
        Assert.NotEqual(firstHeir, House(kernel).HeirName);
        Assert.Equal(1, kernel.Db.ScalarLong("SELECT COUNT(*) FROM ln_child WHERE is_heir = 1"));
    }

    [Fact]
    public void Designating_a_stranger_as_heir_is_refused()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");

        var message = Command(kernel, "heir:999");

        Assert.Contains("Không có người con này", message);
    }

    [Fact]
    public void Monthly_upkeep_is_charged_at_the_boundary()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");

        var upkeep = House(kernel).MonthlyUpkeep;
        Assert.True(upkeep > 0);

        var before = kernel.Ledger.Balance;
        kernel.AdvanceDays(31);

        Assert.Equal(before - upkeep, kernel.Ledger.Balance);
        Assert.Contains(kernel.Ledger.Recent(20), line => line.JournalCode == JournalCodes.FamilyUpkeep);
    }

    [Fact]
    public void Unmarried_player_pays_no_upkeep()
    {
        using var kernel = _host.Boot();
        var before = kernel.Ledger.Balance;

        kernel.AdvanceDays(31);

        Assert.Equal(0, House(kernel).MonthlyUpkeep);
        Assert.Equal(before, kernel.Ledger.Balance);
    }

    [Fact]
    public void Invariants_hold_after_a_full_family_cycle()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");
        Command(kernel, "beget");
        var child = kernel.Db.Query("SELECT id FROM ln_child ORDER BY id LIMIT 1", row => row.GetInt64(0)).Single();
        Command(kernel, $"heir:{child}");
        kernel.AdvanceDays(65);

        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Shrine_defaults_to_level_1_and_can_be_upgraded()
    {
        using var kernel = _host.Boot();
        var house = House(kernel);

        Assert.Equal(1, house.ShrineLevel);
        Assert.Equal(10, house.ClanPrestige);

        var balanceBefore = kernel.Ledger.Balance;
        var msg = Command(kernel, "upgrade_shrine");

        Assert.Contains("Đại công cáo thành", msg);
        Assert.Equal(2, house.ShrineLevel);
        Assert.Equal(60, house.ClanPrestige);
        Assert.Equal(balanceBefore - 500, kernel.Ledger.Balance);

        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Hold_offering_charges_fee_and_increases_clan_prestige()
    {
        using var kernel = _host.Boot();
        var house = House(kernel);
        var initialPrestige = house.ClanPrestige;
        var balanceBefore = kernel.Ledger.Balance;

        var msg = Command(kernel, "hold_offering");
        Assert.Contains("tế tổ trang nghiêm", msg);
        Assert.Equal(initialPrestige + 15, house.ClanPrestige);
        Assert.Equal(balanceBefore - 80, kernel.Ledger.Balance);

        // Immediate repeat is refused
        var repeatMsg = Command(kernel, "hold_offering");
        Assert.Contains("cách quãng", repeatMsg);

        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Child_tutoring_and_teaching_boosts_stats()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");

        var house = House(kernel);
        var child = house.Children.First();

        // Assign tutor
        var tutorMsg = Command(kernel, $"tutor:{child.Id}:WAR");
        Assert.Contains("giáo dưỡng (WAR)", tutorMsg);

        var balanceBefore = kernel.Ledger.Balance;
        // Teach 3 times to gain +1 stat bonus
        for (var i = 0; i < 3; i++)
        {
            var teachMsg = Command(kernel, $"teach:{child.Id}");
            Assert.Contains("chuyên cần học tập", teachMsg);
        }

        Assert.Equal(balanceBefore - (25 * 3), kernel.Ledger.Balance);

        var updatedChild = house.Children.First(c => c.Id == child.Id);
        Assert.NotNull(updatedChild.MentorName);
        Assert.Equal("WAR", updatedChild.FocusStat);
        Assert.Equal(1, updatedChild.StatBonus);

        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void The_same_seed_produces_the_same_spouse()
    {
        using var first = new TestHost("simt-lineage-a");
        using var second = new TestHost("simt-lineage-b");

        Assert.Equal(MarryAndReadSpouse(first), MarryAndReadSpouse(second));
    }

    [Fact]
    public void Spouse_names_use_han_era_surnames_and_do_not_collide_with_300_npcs()
    {
        using var kernel = _host.Boot();
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npcNames = npcs.All
            .Select(n => SimT.Contracts.Scenes.RoleTitleVi.ShortName(n.NameVi))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(["Vương Thị", "Lưu Thị", "Tôn Thị", "Mã Thị", "Hà Thị"], LineageService.SpouseNames);
        foreach (var spouseName in LineageService.SpouseNames)
        {
            Assert.DoesNotContain(spouseName, npcNames);
        }

        Command(kernel, "marry");
        Assert.Contains(House(kernel).SpouseName, LineageService.SpouseNames);
    }

    [Fact]
    public void Child_ages_and_advances_to_adulthood()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");

        var house = House(kernel);
        Assert.Equal(1, house.ChildCount);
        var child = house.Children[0];
        Assert.Equal(0, child.Age);
        Assert.False(child.IsAdult);

        // Advance 16 years (16 * 360 = 5760 days)
        kernel.AdvanceDays(5760);

        var childGrown = house.Children[0];
        Assert.True(childGrown.Age >= 16);
        Assert.True(childGrown.IsAdult);
    }

    [Fact]
    public void Dynasty_family_tree_renders_generational_nodes_and_succession()
    {
        using var kernel = _host.Boot();
        Command(kernel, "marry");
        Command(kernel, "beget");

        var screen = kernel.Ui.Screens.First(s => s.ScreenId == "lineage.family");
        var model = screen.Render();

        var treeSection = model.Sections.First(s => s.Heading.Contains("Cây Phả Hệ Gia Tộc"));
        Assert.NotEmpty(treeSection.Rows);

        // Generation 1 row
        var gen1 = treeSection.Rows[0];
        Assert.Equal("Đời Thứ Nhất", gen1.Cells[0]);
        Assert.Contains("Gia Chủ & Chính Thất", gen1.Cells[1]);

        // Generation 2 row
        var gen2 = treeSection.Rows[1];
        Assert.Equal("Đời Thứ Hai", gen2.Cells[0]);
        Assert.Contains("👦", gen2.Cells[1]);
    }

    [Fact]
    public void Marriage_to_npc_requires_affinity_and_records_bond()
    {
        using var kernel = _host.Boot();
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var target = npcs.All[0];

        // Low affinity: refused
        var lowAffinityMsg = Command(kernel, $"marry:{target.Id}");
        Assert.Contains("Hảo cảm", lowAffinityMsg);

        // Raise affinity to 85 via direct DB or relationship
        kernel.Db.Execute(
            "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) VALUES (@npc, 85, 80, 'Tri kỷ', 5, '184-05-01') ON CONFLICT(npc_id) DO UPDATE SET affinity = 85",
            SimT.Contracts.Data.DbArgs.One("@npc", target.Id));

        var marryMsg = Command(kernel, $"marry:{target.Id}");
        Assert.Contains("Thành hôn", marryMsg);

        var house = House(kernel);
        Assert.True(house.IsMarried);
        Assert.Equal(target.NameVi, house.SpouseName);
        Assert.Equal(target.Id, house.SpouseNpcId);

        // Verify spouse npc_id recorded in ln_spouse
        var recordedNpcId = kernel.Db.ScalarLong(
            "SELECT npc_id FROM ln_spouse ORDER BY id LIMIT 1");
        Assert.Equal(target.Id, recordedNpcId);
    }

    [Fact]
    public void Marriage_to_npc_respects_existing_spouses()
    {
        using var kernel = _host.Boot();
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var target = npcs.All[0];

        // Record target as already married to another valid NPC
        var otherNpc = npcs.All[1];
        var minId = Math.Min(target.Id, otherNpc.Id);
        var maxId = Math.Max(target.Id, otherNpc.Id);
        kernel.Db.Execute(
            "INSERT INTO kz_npc_bond (npc_a, npc_b, kind, strength, since_date, source) VALUES (@a, @b, 'spouse', 100, '184-01-01', 'history')",
            new SimT.Contracts.Data.DbArgs().Add("@a", minId).Add("@b", maxId));

        kernel.Db.Execute(
            "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) VALUES (@npc, 90, 80, 'Tri kỷ', 5, '184-05-01') ON CONFLICT(npc_id) DO UPDATE SET affinity = 90",
            SimT.Contracts.Data.DbArgs.One("@npc", target.Id));

        var marryMsg = Command(kernel, $"marry:{target.Id}");
        Assert.Contains("đã có hôn phối", marryMsg);
        Assert.False(House(kernel).IsMarried);
    }

    private static string MarryAndReadSpouse(TestHost host)
    {
        using var kernel = host.Boot();
        Command(kernel, "marry");
        return House(kernel).SpouseName ?? string.Empty;
    }

    private static string Command(SimKernel kernel, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler("lineage.family", out var handler), "lineage screen is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    private static ILineageHouse House(SimKernel kernel) =>
        kernel.Capabilities.Get<ILineageHouse>(CapabilityIds.LineageHouse);
}
