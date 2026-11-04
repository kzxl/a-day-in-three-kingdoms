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
    public void NpcRegistry_contains_all_127_characters()
    {
        using var kernel = _host.Boot();
        var registry = Registry(kernel);

        Assert.Equal(127, registry.All.Count);

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
}
