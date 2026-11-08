using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class NpcRegistryTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static INpcRegistry Registry(SimKernel kernel) =>
        kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

    [Fact]
    public void NpcRegistry_preserves_300_catalog_entries_and_filters_underage_roster()
    {
        using var kernel = _host.Boot();
        var registry = Registry(kernel);

        Assert.Equal(300, kernel.Db.ScalarLong("SELECT COUNT(*) FROM np_npc"));
        Assert.InRange(registry.All.Count, 1, 299);

        // Availability follows verified ages rather than an artificial 20 per city quota.
        for (long cid = 1; cid <= 15; cid++)
        {
            var citNpcs = registry.FindByCitadel(cid);
            Assert.All(citNpcs, npc => Assert.Contains(npc, registry.All));
        }

        // 2 baseline stub characters preserved
        var stubMerchant = registry.Find(1);
        Assert.NotNull(stubMerchant);
        Assert.Equal("thuong_nhan_a", stubMerchant.Code);
        Assert.Equal("Trương Lão Bản", stubMerchant.NameVi);
        Assert.Equal(85, stubMerchant.Attributes.Com);
        Assert.Equal(65, stubMerchant.Attributes.Con);

        // Historical characters
        var liuBei = registry.FindByCode("npc_0001");
        Assert.NotNull(liuBei);
        Assert.Equal("Lưu Bị (Huyền Đức)", liuBei.NameVi);
        Assert.Equal("Huyền Đức", liuBei.StyleNameVi);
        Assert.Equal("刘备", liuBei.NameHan);
        Assert.Equal(4, liuBei.CitadelId); // Trác Huyện
        Assert.Equal(86, liuBei.Attributes.Con);
        Assert.Equal(98, liuBei.Attributes.Wil);
        Assert.Equal(90, liuBei.Attributes.Lck);

        // Expanded cast characters (IDs 128 to 300)
        var caoZhi = registry.FindByCode("npc_0221");
        Assert.NotNull(caoZhi);
        Assert.Equal("Tào Thực (Tử Kiến)", caoZhi.NameVi);
        Assert.Equal(1, caoZhi.CitadelId);

        var maDeng = registry.FindByCode("npc_0231");
        Assert.NotNull(maDeng);
        Assert.Equal("Mã Đằng (Thọ Thành)", maDeng.NameVi);
        Assert.Equal(2, maDeng.CitadelId);
    }

    [Fact]
    public void NpcRegistry_registers_dynamic_npcs_at_runtime()
    {
        using var kernel = _host.Boot();
        var registry = Registry(kernel);

        var initialCount = registry.All.Count;
        Assert.InRange(initialCount, 1, 299);
        var localCount = registry.FindByCitadel(1).Count;

        var newNpc = registry.RegisterDynamicNpc(
            "npc_custom_test",
            "Võ Sĩ Ẩn Danh",
            "Du Hiệp",
            1,
            new CharacterAttributes(70, 60, 50, 40, 50, 50, 50, 50, 60, 60, 50, 50),
            "Khảng khái",
            "Một hiệp khách giang hồ.");

        Assert.NotNull(newNpc);
        Assert.Equal(initialCount + 1, registry.All.Count);
        Assert.Equal("Võ Sĩ Ẩn Danh", newNpc.NameVi);
        Assert.Equal("npc_custom_test", newNpc.Code);
        Assert.Equal(301, newNpc.Id);

        var retrieved = registry.Find(newNpc.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Võ Sĩ Ẩn Danh", retrieved.NameVi);

        var cit1Npcs = registry.FindByCitadel(1);
        Assert.Equal(localCount + 1, cit1Npcs.Count);
        Assert.Contains(cit1Npcs, n => n.Id == newNpc.Id);
    }

    [Fact]
    public void NpcRegistry_all_characters_have_unique_names_and_expanded_cast()
    {
        using var kernel = _host.Boot();
        var registry = Registry(kernel);

        // Verify zero duplicate names across all 127 NPCs
        var duplicateNames = registry.All
            .GroupBy(n => n.NameVi)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.Empty(duplicateNames);

        // Verify newly added historical figures and their unique skills
        var huangYueYing = registry.FindByCode("npc_0137");
        Assert.NotNull(huangYueYing);
        Assert.Equal("Hoàng Nguyệt Anh", huangYueYing.NameVi);
        Assert.Equal(99, huangYueYing.Attributes.Cra);
        Assert.Equal(95, huangYueYing.Attributes.Int);
        Assert.Equal(12, huangYueYing.CitadelId); // Giang Lăng
        Assert.Contains(huangYueYing.Skills, s => s.Code == "co_quan");

        var zhangChunHua = registry.FindByCode("npc_0142");
        Assert.NotNull(zhangChunHua);
        Assert.Equal("Trương Xuân Hoa", zhangChunHua.NameVi);
        Assert.Equal(90, zhangChunHua.Attributes.Int);
        Assert.Equal(95, zhangChunHua.Attributes.Wil);
        Assert.Contains(zhangChunHua.Skills, s => s.Code == "quyet_doan");

        var gaoShun = registry.FindByCode("npc_0133");
        Assert.NotNull(gaoShun);
        Assert.Equal("Cao Thuận", gaoShun.NameVi);
        Assert.Equal(9, gaoShun.CitadelId); // Hạ Phì
        Assert.Contains(gaoShun.Skills, s => s.Code == "ham_tran_doanh");
    }

    [Fact]
    public void NpcRegistry_preserves_canonical_twelve_attributes()
    {
        using var kernel = _host.Boot();
        var registry = Registry(kernel);

        // Quan Vũ: WAR=98, CON=92, WIL=96
        var guanYu = registry.FindByCode("npc_0003");
        Assert.NotNull(guanYu);
        Assert.Equal("Quan Vũ (Vân Trường)", guanYu.NameVi);
        Assert.Equal(98, guanYu.Attributes.War);
        Assert.Equal(92, guanYu.Attributes.Con);
        Assert.Equal(96, guanYu.Attributes.Wil);

        // Gia Cát Lượng: INT=100, PRC=99, WIL=100
        var zhugeLiang = registry.FindByCode("npc_0025");
        Assert.NotNull(zhugeLiang);
        Assert.Equal("Gia Cát Lượng (Khổng Minh)", zhugeLiang.NameVi);
        Assert.Equal(100, zhugeLiang.Attributes.Int);
        Assert.Equal(99, zhugeLiang.Attributes.Prc);
        Assert.Equal(100, zhugeLiang.Attributes.Wil);
    }

    [Fact]
    public void NpcRegistry_filters_by_citadel()
    {
        using var kernel = _host.Boot();
        var registry = Registry(kernel);

        // Trác Huyện (citadel 4) should contain Liu Bei, Guan Yu, Zhang Fei
        var zhuoNpcs = registry.FindByCitadel(4);
        Assert.NotEmpty(zhuoNpcs);
        Assert.Contains(zhuoNpcs, n => n.NameVi.Contains("Lưu Bị"));
        Assert.Contains(zhuoNpcs, n => n.NameVi.Contains("Trương Phi"));
        Assert.Contains(zhuoNpcs, n => n.NameVi.Contains("Quan Vũ"));
    }

    [Fact]
    public void NpcDirectoryScreen_is_registered_and_renders_properly()
    {
        using var kernel = _host.Boot();
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "npc.directory");
        Assert.NotNull(screen);
        Assert.Equal("Nhân Sĩ", screen.Title);

        var model = screen.Render();
        Assert.NotEmpty(model.Sections);
        Assert.NotEmpty(model.Headline);

        var allRows = model.Sections.SelectMany(s => s.Rows).ToList();
        Assert.NotEmpty(allRows);
        Assert.Contains(allRows, r => r.Commands.Any(c => c.Id.StartsWith("npc:modal:")));

        Assert.True(kernel.TryGetCommandHandler("npc.directory", out var handler));
        Assert.NotNull(handler);

        var sampleCommand = allRows.First().Commands.First(c => c.Id.StartsWith("npc:modal:"));
        var result = handler.Execute(sampleCommand.Id);
        Assert.NotNull(result);
        Assert.Contains("Đang xem hồ sơ:", result);
    }

    [Fact]
    public void NpcDirectoryScreen_applies_non_godview_veiling_and_lifts_when_met()
    {
        using var kernel = _host.Boot();
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "npc.directory");
        Assert.NotNull(screen);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);

        var localNpc = registry.All.First(n => n.CitadelId == 1);

        // Before meeting: Fog of war applied on local unmet NPC
        var initialModel = screen.Render();
        var localSection = initialModel.Sections.First(s => s.Heading.Contains("đồng thành"));
        var npcRowBefore = localSection.Rows.First(r => r.Cells[0] == localNpc.NameVi);
        Assert.NotNull(npcRowBefore.Details);
        Assert.Contains(npcRowBefore.Details!, d => d.Value.Contains("Cơ mật bản thể"));

        // Meet NPC via chat
        social.ChatWithNpc(localNpc.Id);

        // After meeting: Fog of war lifted, verified stats revealed
        var updatedModel = screen.Render();
        var updatedLocalSection = updatedModel.Sections.First(s => s.Heading.Contains("đồng thành"));
        var npcRowAfter = updatedLocalSection.Rows.First(r => r.Cells[0] == localNpc.NameVi);
        Assert.NotNull(npcRowAfter.Details);
        Assert.Contains(npcRowAfter.Details!, d => d.Label == "Căn Cốt (CON)" && d.Value == localNpc.Attributes.Con.ToString());
    }

    [Fact]
    public void NpcDirectoryScreen_tue_nhan_perception_reveals_stats_at_thresholds()
    {
        using var kernel = _host.Boot();
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "npc.directory");
        Assert.NotNull(screen);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var playerAttrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);

        var localNpc = registry.All.First(n => n.CitadelId == 1);

        // Tier 1 (Default PRC = 60): Narrow estimation (Mẫn Tiệp), innate stats hidden
        Assert.Equal(60, playerAttrs.Attributes.Prc);
        var modelPrc60 = screen.Render();
        Assert.NotNull(modelPrc60.Metrics);
        Assert.Contains(modelPrc60.Metrics!, m => m.Label == "Tuệ Nhãn" && m.Value == "60 điểm");
        var localSec60 = modelPrc60.Sections.First(s => s.Heading.Contains("đồng thành"));
        var row60 = localSec60.Rows.First(r => r.Cells[0] == localNpc.NameVi);
        Assert.StartsWith("~", row60.Cells[4]); // WAR estimation
        Assert.Contains(row60.Details!, d => d.Value.Contains("Cơ mật bản thể"));

        // Chat grants PRC experience
        var initialPrcExp = playerAttrs.Exps.Prc;
        social.ChatWithNpc(localNpc.Id);
        Assert.True(playerAttrs.Exps.Prc > initialPrcExp || playerAttrs.Attributes.Prc > 60);

        // Tier 2 (PRC = 70): Behavioral stats exact, innate stats veiled (< 85)
        // Find another unmet local NPC
        var unmetLocalNpc = registry.All.First(n => n.CitadelId == 1 && n.Id != localNpc.Id);
        playerAttrs.AddExp("PRC", 1000); // 60 -> 70
        Assert.True(playerAttrs.Attributes.Prc >= 70);

        var modelPrc70 = screen.Render();
        Assert.NotNull(modelPrc70.Metrics);
        Assert.Contains(modelPrc70.Metrics!, m => m.Label == "Tuệ Nhãn" && (m.Hint?.Contains("Thông Tuệ") ?? false));
        var localSec70 = modelPrc70.Sections.First(s => s.Heading.Contains("đồng thành"));
        var row70 = localSec70.Rows.First(r => r.Cells[0] == unmetLocalNpc.NameVi);
        Assert.Equal(unmetLocalNpc.Attributes.War.ToString(), row70.Cells[4]); // Exact WAR!
        Assert.Equal(unmetLocalNpc.Attributes.Int.ToString(), row70.Cells[6]); // Exact INT!
        Assert.Contains(row70.Details!, d => d.Label == "Võ Lực (WAR)" && d.Value == unmetLocalNpc.Attributes.War.ToString());
        Assert.Contains(row70.Details!, d => d.Value.Contains("PRC 85+")); // Innate CON still veiled

        // Tier 3 (PRC = 85): 100% full reveal on all local NPCs without meeting
        playerAttrs.AddExp("PRC", 1500); // 70 -> 85
        Assert.True(playerAttrs.Attributes.Prc >= 85);

        var modelPrc85 = screen.Render();
        Assert.NotNull(modelPrc85.Metrics);
        Assert.Contains(modelPrc85.Metrics!, m => m.Label == "Tuệ Nhãn" && (m.Hint?.Contains("Như Đuốc") ?? false));
        var localSec85 = modelPrc85.Sections.First(s => s.Heading.Contains("đồng thành"));
        var row85 = localSec85.Rows.First(r => r.Cells[0] == unmetLocalNpc.NameVi);
        Assert.Equal(unmetLocalNpc.Attributes.War.ToString(), row85.Cells[4]);
        Assert.Contains(row85.Details!, d => d.Label == "Căn Cốt (CON)" && d.Value == unmetLocalNpc.Attributes.Con.ToString());
        Assert.DoesNotContain(row85.Details!, d => d.Value.Contains("Cơ mật bản thể"));
    }

    [Fact]
    public void NpcDirectoryScreen_perception_skills_hybrid_mechanics()
    {
        using var kernel = _host.Boot();
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "npc.directory");
        Assert.NotNull(screen);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var playerAttrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);

        // 1. Verify perception skills exist on master mentors
        var tuMaHuy = registry.Find(229);
        Assert.NotNull(tuMaHuy);
        Assert.Contains(tuMaHuy.Skills, s => s.Code == "ba_lac_than_nhan");

        var tuanUc = registry.Find(34);
        Assert.NotNull(tuanUc);
        Assert.Contains(tuanUc.Skills, s => s.Code == "nguyet_dan_giam_dinh");

        var giaCatLuong = registry.Find(24);
        Assert.NotNull(giaCatLuong);
        Assert.Contains(giaCatLuong.Skills, s => s.Code == "vong_khi_thien_co");

        // 2. Base PRC without skills
        Assert.Equal(60, playerAttrs.Attributes.Prc);
        Assert.Equal(60, playerAttrs.EffectivePrc);

        // 3. Learn & Equip Ba Lac Than Nhan (+15 Effective PRC)
        playerAttrs.LearnSkill("ba_lac_than_nhan");
        playerAttrs.EquipSkill("ba_lac_than_nhan", 1, 3);
        Assert.True(playerAttrs.IsSkillEquipped("ba_lac_than_nhan"));
        Assert.Equal(75, playerAttrs.EffectivePrc);

        // Model now shows Ba Lac enhanced insight (even on distant renowned NPCs)
        var modelBaLac = screen.Render();
        Assert.Contains(modelBaLac.Metrics!, m => m.Label == "Tuệ Nhãn" && m.Value.Contains("75 điểm (+15 Kỹ Năng)"));

        var unmetLocalNpc = registry.All.First(n => n.CitadelId == 1 && n.Id != 1);
        var localSec = modelBaLac.Sections.First(s => s.Heading.Contains("đồng thành"));
        var localRow = localSec.Rows.First(r => r.Cells[0] == unmetLocalNpc.NameVi);
        Assert.Equal(unmetLocalNpc.Attributes.War.ToString(), localRow.Cells[4]); // Exact WAR via Ba Lac

        // 4. Learn & Equip Nguyet Dan Giam Dinh (+25 Effective PRC)
        playerAttrs.LearnSkill("nguyet_dan_giam_dinh");
        playerAttrs.EquipSkill("nguyet_dan_giam_dinh", 2, 4);
        Assert.True(playerAttrs.IsSkillEquipped("nguyet_dan_giam_dinh"));
        Assert.Equal(100, playerAttrs.EffectivePrc); // 60 + 15 + 25 = 100

        // Chat with local NPC trains Nguyet Dan & gives extra affinity
        var initialProf = playerAttrs.GetSkillProgress("nguyet_dan_giam_dinh")?.Proficiency ?? 0;
        var chatResult = social.ChatWithNpc(unmetLocalNpc.Id);
        Assert.Contains("Nguyệt Đán Giám Định", chatResult);
        var updatedProf = playerAttrs.GetSkillProgress("nguyet_dan_giam_dinh")?.Proficiency ?? 0;
        Assert.True(updatedProf > initialProf);

        // 5. Learn & Equip Vong Khi Thien Co (+40 Effective PRC -> Full 100% reveal)
        playerAttrs.LearnSkill("vong_khi_thien_co");
        playerAttrs.EquipSkill("vong_khi_thien_co", 3, 5);
        Assert.True(playerAttrs.IsSkillEquipped("vong_khi_thien_co"));
        Assert.Equal(140, playerAttrs.EffectivePrc); // 60 + 15 + 25 + 40 = 140

        var modelVongKhi = screen.Render();
        Assert.Contains(modelVongKhi.Metrics!, m => m.Label == "Tuệ Nhãn" && (m.Hint?.Contains("Thiên Nhãn Toàn Tri") ?? false));
        var localSecVongKhi = modelVongKhi.Sections.First(s => s.Heading.Contains("đồng thành"));
        var localRowVongKhi = localSecVongKhi.Rows.First(r => r.Cells[0] == unmetLocalNpc.NameVi);
        Assert.Contains(localRowVongKhi.Details!, d => d.Label == "Căn Cốt (CON)" && d.Value == unmetLocalNpc.Attributes.Con.ToString());
    }
}
