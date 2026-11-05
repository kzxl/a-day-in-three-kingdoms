using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using Xunit;

namespace SimT.Core.Tests;

public sealed class SceneContractsTests
{
    [Fact]
    public void SceneTrustGuard_AcceptsReadyAndClose_RejectsUnknownTypes()
    {
        Assert.True(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("ready"), null));
        Assert.True(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("close"), null));
        Assert.False(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("eval", Id: "1"), null));
        Assert.False(SceneTrustGuard.IsAllowedMessage(null, null));
    }

    [Fact]
    public void SceneTrustGuard_ValidatesChoiceAndNavigationAgainstActiveScene()
    {
        var talk = new TalkScene(
            SceneId: "talk:1",
            NpcId: 1,
            NpcCode: "npc_0001",
            NameVi: "Lưu Bị",
            NameHan: "劉備",
            StyleNameVi: "Huyền Đức",
            RoleVi: "Võ tướng",
            PersonalityVi: "Nhân nghĩa",
            LocationNameVi: "Lạc Dương",
            LocationHan: "洛陽",
            Affinity: 45,
            RelationTierVi: "Thân thiết",
            PortraitFull: "art/portraits/liu_bei/full.webp",
            PortraitFace: "art/portraits/liu_bei/face.webp",
            BackgroundPath: "art/backgrounds/street.png",
            Lines: ["Thiên hạ đại loạn, chỉ mong an dân."],
            Choices:
            [
                new SceneChoice("chat", "Hỏi chuyện thành này", Enabled: true),
                new SceneChoice("gift", "Tặng quà", Enabled: false, DisabledReasonVi: "Túi trống")
            ],
            NearbyNpcs:
            [
                new SceneNpcThumb(1, "Lưu Bị", "art/portraits/liu_bei/face.webp", true),
                new SceneNpcThumb(3, "Quan Vũ", "art/portraits/guan_yu/face.webp", false)
            ]);

        Assert.True(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("choice", "talk:1", "chat"), talk));
        Assert.False(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("choice", "talk:1", "gift"), talk));
        Assert.False(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("choice", "talk:1", "hack"), talk));
        Assert.True(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("navigate", "talk:1", "npc:3"), talk));
        Assert.False(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("navigate", "talk:1", "npc:999"), talk));
    }

    [Fact]
    public void SceneArtCatalog_ResolvesNamedNpcAndFallbackArchetype()
    {
        var liuBei = SceneArtCatalog.ResolveNpcArt("npc_0001", "general");
        Assert.Equal("art/portraits/liu_bei/full.webp", liuBei.PortraitFull);
        Assert.Equal("art/portraits/liu_bei/face.webp", liuBei.PortraitFace);
        Assert.Equal("art/sprites/liu_bei.png", liuBei.SpritePath);

        var merchant = SceneArtCatalog.ResolveNpcArt("npc_9999", "merchant");
        Assert.Equal("art/portraits/merchant/full.webp", merchant.PortraitFull);
        Assert.Equal("art/sprites/traveler.png", merchant.SpritePath);
        Assert.Equal("Thương nhân", RoleTitleVi.Format("merchant"));
        Assert.Equal("Chưởng quỹ quán trọ", RoleTitleVi.Format("tavern"));
    }
}
