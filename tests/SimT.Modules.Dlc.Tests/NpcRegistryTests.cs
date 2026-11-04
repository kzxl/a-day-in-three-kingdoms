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
}
