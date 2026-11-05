using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Ui;
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
        Assert.Equal("art/sprites/civ_merchant.png", merchant.SpritePath);
        Assert.Equal("Thương nhân", RoleTitleVi.Format("merchant"));
        Assert.Equal("Chưởng quỹ quán trọ", RoleTitleVi.Format("tavern"));

        var duel = new DuelScene(
            SceneId: "duel:2",
            NpcId: 2,
            NpcCode: "npc_0002",
            TitleVi: "Tỷ võ cùng Trương Phi",
            CitadelNameVi: "Lạc Dương",
            BackgroundPath: "art/backgrounds/arena.png",
            Player: new DuelFighterView("Lữ Khách", "Bộ Binh", SceneArtCatalog.PlayerPortraitFace, SceneArtCatalog.PlayerSprite, TroopBranch.Infantry, 125, 125, 0, 100),
            Opponent: new DuelFighterView("Trương Phi", "Kỵ Binh", "art/portraits/zhang_fei/face.webp", "art/sprites/zhang_fei.png", TroopBranch.Cavalry, 145, 145, 10, 100),
            MatchupSummaryVi: "Kỵ Binh khắc Bộ Binh",
            Result: null,
            Choices: [new SceneChoice("branch:Infantry", "Xuất chiến Bộ Binh")]);

        Assert.True(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("choice", "duel:2", "branch:Infantry"), duel));
        Assert.False(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("choice", "duel:2", "branch:Invalid"), duel));
    }

    [Fact]
    public void SceneArtCatalog_UnifiesSceneAndWpfRowPortraitResolution_ForThaiDiemHoaHungTuMaY()
    {
        Assert.True(SceneArtCatalog.IsManifestLoadedFromDisk);

        // 1. Thái Diễm -> lady portrait
        var thaiDiemRow = new ScreenRow(
            ["Thái Diễm (Thái Văn Cơ)", "Thái Văn Cơ (蔡琰)", "Hồ Gia Thập Bát Phách • Tuyệt Đại Tài Nữ", "Lạc Dương", "35", "40", "92", "65"],
            [new ScreenCommand("talk:npc:94", "Bái phỏng"), new ScreenCommand("npc:modal:94", "Hồ Sơ")],
            Title: "Thái Diễm (Thái Văn Cơ) — Thái Văn Cơ",
            Details: [new ScreenDetail("Thân Phận", "Hồ Gia Thập Bát Phách • Tuyệt Đại Tài Nữ")]);
        var thaiDiemMeta = SceneArtCatalog.ExtractRowNpcMetadata(thaiDiemRow);
        Assert.Equal("lady", thaiDiemMeta.Archetype);
        Assert.Equal("art/portraits/lady/face.webp", thaiDiemMeta.PortraitFace);

        // 2. Hoa Hùng -> general portrait
        var hoaHungRow = new ScreenRow(
            ["Hoa Hùng", "Hoa Hùng (華雄)", "Tây Lương Kiêu Tướng • Trảm Tướng Tị Thủy Quan", "Lạc Dương", "92", "80", "45", "30"],
            [new ScreenCommand("talk:npc:92", "Bái phỏng"), new ScreenCommand("npc:modal:92", "Hồ Sơ")],
            Title: "Hoa Hùng — Hoa Hùng",
            Details: [new ScreenDetail("Thân Phận", "Tây Lương Kiêu Tướng • Trảm Tướng Tị Thủy Quan")]);
        var hoaHungMeta = SceneArtCatalog.ExtractRowNpcMetadata(hoaHungRow);
        Assert.Equal("general", hoaHungMeta.Archetype);
        Assert.Equal("art/portraits/general/face.webp", hoaHungMeta.PortraitFace);

        // 3. Tư Mã Ý -> scholar portrait
        var tuMaYRow = new ScreenRow(
            ["Tư Mã Ý (Trọng Đạt)", "Trọng Đạt (司馬懿)", "Tấn Cao Tổ • Lang Cố Chi Tướng Trấn Thủ Trung Nguyên", "Lạc Dương", "68", "96", "98", "82"],
            [new ScreenCommand("talk:npc:38", "Bái phỏng"), new ScreenCommand("npc:modal:38", "Hồ Sơ")],
            Title: "Tư Mã Ý (Trọng Đạt) — Trọng Đạt",
            Details: [new ScreenDetail("Thân Phận", "Tấn Cao Tổ • Lang Cố Chi Tướng Trấn Thủ Trung Nguyên")]);
        var tuMaYMeta = SceneArtCatalog.ExtractRowNpcMetadata(tuMaYRow);
        Assert.Equal("scholar", tuMaYMeta.Archetype);
        Assert.Equal("art/portraits/scholar/face.webp", tuMaYMeta.PortraitFace);
    }
}
