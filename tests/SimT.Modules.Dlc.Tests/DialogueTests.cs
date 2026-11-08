using System.Text.Json;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Core.Kernel;
using SimT.Core.Modules;
using SimT.Testing;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class DialogueTests
{
    [Fact]
    public void Selects_dialogue_lines_by_relationship_tier_for_named_and_role_npcs()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();

        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var liuBei = npcs.FindByCode("npc_0001")!;

        var strangerLines = dialogue.GetLinesForNpc(liuBei.Id, "stranger");
        var acquaintanceLines = dialogue.GetLinesForNpc(liuBei.Id, "acquaintance");
        var trustedLines = dialogue.GetLinesForNpc(liuBei.Id, "trusted");

        Assert.True(strangerLines.Count >= 3);
        Assert.True(acquaintanceLines.Count >= 3);
        Assert.True(trustedLines.Count >= 3);
        Assert.NotEqual(strangerLines[0], acquaintanceLines[0]);
        Assert.NotEqual(acquaintanceLines[0], trustedLines[0]);
    }

    [Fact]
    public void Every_dialogue_line_is_at_most_90_chars_and_every_choice_is_at_most_40_chars()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();

        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var playerState = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        bag.Add(playerState.CurrentCityId, 1, 2);

        foreach (var npc in npcs.All.Take(40))
        {
            foreach (var tier in new[] { "stranger", "acquaintance", "trusted" })
            {
                var lines = dialogue.GetLinesForNpc(npc.Id, tier);
                Assert.NotEmpty(lines);
                Assert.All(lines, line => Assert.True(line.Length <= 90, $"Line > 90 chars ({line.Length}): {line}"));
            }

            var scene = dialogue.BuildTalkScene(npc.Id);
            Assert.All(scene.Lines, line => Assert.True(line.Length <= 90, $"Scene line > 90 chars: {line}"));
            Assert.All(scene.Choices, c => Assert.True(c.LabelVi.Length <= 40, $"Choice > 40 chars: {c.LabelVi}"));
        }
    }

    [Fact]
    public void Gift_choice_opens_item_submenu_sorted_by_price_and_supports_item_selection_and_cancel()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();

        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var playerState = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var localNpc = npcs.FindByCitadel(playerState.CurrentCityId).First();
        var cityId = playerState.CurrentCityId;

        var emptyBagScene = dialogue.BuildTalkScene(localNpc.Id);
        Assert.DoesNotContain(emptyBagScene.Choices, c => c.Id == "gift");

        var distinctItems = items.All.Take(6).ToList();
        foreach (var it in distinctItems)
            bag.Add(cityId, it.Id, 2);

        var withItemScene = dialogue.BuildTalkScene(localNpc.Id);
        Assert.Contains(withItemScene.Choices, c => c.Id == "gift");

        // Clicking "gift" opens the submenu with up to 5 highest-BasePrice items + "gift:cancel"
        var giftMenuOutcome = dialogue.HandleChoice(localNpc.Id, "gift");
        Assert.True(giftMenuOutcome.Ok);
        Assert.True(giftMenuOutcome.MessageVi.Length <= 90);
        Assert.NotNull(giftMenuOutcome.UpdatedScene);

        var menuChoices = giftMenuOutcome.UpdatedScene!.Choices;
        Assert.Equal(6, menuChoices.Count);
        Assert.Equal("gift:cancel", menuChoices[^1].Id);
        Assert.Equal("Thôi", menuChoices[^1].LabelVi);

        var expectedTop5 = distinctItems
            .OrderByDescending(i => i.BasePrice)
            .ThenBy(i => i.Id)
            .Take(5)
            .Select(i => $"gift:{i.Id}")
            .ToArray();
        Assert.Equal(expectedTop5, menuChoices.Take(5).Select(c => c.Id).ToArray());

        // Cancel returns to normal choices without consuming items
        var cancelOutcome = dialogue.HandleChoice(localNpc.Id, "gift:cancel");
        Assert.True(cancelOutcome.Ok);
        Assert.NotNull(cancelOutcome.UpdatedScene);
        Assert.Contains(cancelOutcome.UpdatedScene!.Choices, c => c.Id == "gift");

        // Selecting a specific gift:<itemId> consumes 1 of that item and increases affinity
        var chosenItem = distinctItems.OrderByDescending(i => i.BasePrice).First();
        var qtyBefore = bag.Quantity(cityId, chosenItem.Id);
        var sendGiftOutcome = dialogue.HandleChoice(localNpc.Id, $"gift:{chosenItem.Id}");
        Assert.True(sendGiftOutcome.Ok);
        Assert.Equal(qtyBefore - 1, bag.Quantity(cityId, chosenItem.Id));
    }

    [Fact]
    public void Disabling_dlc_dialogue_still_boots_kernel_cleanly()
    {
        var dir = Path.Combine(Path.GetTempPath(), "simt-no-dialogue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var modulesWithoutDialogue = ModuleLoader.Discover(AppContext.BaseDirectory)
                .Select(d => d.Manifest.Id)
                .Where(id => !string.Equals(id, "dlc.character", StringComparison.Ordinal))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            var productPath = Path.Combine(dir, "product.json");
            File.WriteAllText(productPath, JsonSerializer.Serialize(new
            {
                id = "test-no-dialogue",
                name = "No Dialogue",
                modules = modulesWithoutDialogue,
                seed = 1840502,
                startLocation = "lac_duong",
                startingCash = 2000
            }));

            using var kernel = SimKernel.Open(new KernelOptions(
                DatabasePath: Path.Combine(dir, "campaign.db"),
                ModulesDirectory: AppContext.BaseDirectory,
                ProductPath: productPath,
                ConstantsPath: TestHost.ConstantsPath,
                BackupDirectory: Path.Combine(dir, "backups")));

            Assert.False(kernel.Capabilities.IsProvided(CapabilityIds.Dialogue));
            Assert.True(kernel.Capabilities.IsProvided(CapabilityIds.NpcRegistry));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Classifies_hua_xiong_zhang_laoban_diaochan_and_sima_yi_to_correct_archetypes_and_lines()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();

        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var huaXiong = npcs.FindByCode("npc_0095")!;
        var zhangLaoban = npcs.FindByCode("thuong_nhan_a")!;
        var diaoChan = npcs.FindByCode("npc_0140")!;
        var simaYi = npcs.FindByCode("npc_0039")!;
        var caiYan = npcs.FindByCode("npc_0097")!;

        Assert.Equal("general", SceneArtCatalog.ClassifyArchetype(huaXiong));
        Assert.Equal("merchant", SceneArtCatalog.ClassifyArchetype(zhangLaoban));
        Assert.Equal("lady", SceneArtCatalog.ClassifyArchetype(diaoChan));
        Assert.Equal("scholar", SceneArtCatalog.ClassifyArchetype(simaYi));
        Assert.Equal("lady", SceneArtCatalog.ClassifyArchetype(caiYan));

        var caiYanScene = dialogue.BuildTalkScene(caiYan.Id);
        Assert.Equal("art/portraits/lady_v2/full.webp", caiYanScene.PortraitFull);
        Assert.Equal("art/portraits/lady_v2/face.webp", caiYanScene.PortraitFace);

        var npcScreen = kernel.Ui.Screens.Single(s => s.ScreenId == "npc.directory").Render();
        var localRows = npcScreen.Sections.First().Rows;
        var caiYanRow = localRows.Single(r => r.Cells[0].Contains("Thái Diễm", StringComparison.Ordinal));
        var huaXiongRow = localRows.Single(r => r.Cells[0].Contains("Hoa Hùng", StringComparison.Ordinal));
        Assert.DoesNotContain(localRows, r => r.Cells[0].Contains("Tư Mã Ý", StringComparison.Ordinal)); // born 179, not yet 15

        Assert.Equal("art/portraits/lady/face.webp", SceneArtCatalog.ResolveRowNpcArt(caiYanRow).PortraitFace);
        Assert.Equal("art/portraits/general/face.webp", SceneArtCatalog.ResolveRowNpcArt(huaXiongRow).PortraitFace);

        Assert.Equal("art/portraits/lady_v2/face.webp", SceneArtCatalog.ResolveRowNpcArt(caiYanRow, npcs).PortraitFace);
        Assert.Equal("art/portraits/general_v3/face.webp", SceneArtCatalog.ResolveRowNpcArt(huaXiongRow, npcs).PortraitFace);

        var huaXiongScene = dialogue.BuildTalkScene(huaXiong.Id);
        Assert.Equal("art/portraits/general_v3/full.webp", huaXiongScene.PortraitFull);
        Assert.Equal("Dũng mãnh", huaXiongScene.PersonalityVi);
        Assert.DoesNotContain(huaXiongScene.Lines, l => l.Contains("Dân đen chúng tôi", StringComparison.Ordinal));

        var simaYiScene = dialogue.BuildTalkScene(simaYi.Id);
        Assert.Equal("art/portraits/scholar/full.webp", simaYiScene.PortraitFull);
        Assert.Equal("Nho nhã", simaYiScene.PersonalityVi);

        Assert.Equal("Trung Nguyên", RoleTitleVi.FormatRegion("trung_nguyen"));
        Assert.Equal("Quan Trung", RoleTitleVi.FormatRegion("quan_trung"));
    }

    [Fact]
    public void Talk_scene_uses_player_current_city_and_local_nearby_npcs_when_npc_home_is_another_citadel()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();

        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var playerState = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society);

        var cityA = map.Find(playerState.CurrentCityId)!;
        var guanYu = npcs.FindByCode("npc_0003")!;
        var zhangFei = npcs.FindByCode("npc_0002")!;
        Assert.NotEqual(cityA.Id, guanYu.CitadelId);

        var scene = dialogue.BuildTalkScene(guanYu.Id);

        Assert.Equal(cityA.NameVi, scene.LocationNameVi);
        Assert.Contains(scene.NearbyNpcs, n => n.NpcId == guanYu.Id && n.Active);
        Assert.DoesNotContain(scene.NearbyNpcs, n => n.NpcId == zhangFei.Id);

        var otherThumbs = scene.NearbyNpcs
            .Where(n => n.NpcId != guanYu.Id)
            .ToList();
        Assert.NotEmpty(otherThumbs);
        foreach (var thumb in otherThumbs)
        {
            var def = npcs.Find(thumb.NpcId)!;
            Assert.Equal(cityA.Id, def.CurrentCitadelId(society));
        }
    }

    [Fact]
    public void Luoyang_hides_guan_yu_zhuo_shows_him_and_visiting_npc_can_chat()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();

        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var playerState = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var guanYu = npcs.FindByCode("npc_0003")!;
        var luoyangId = playerState.CurrentCityId;
        var zhuoId = guanYu.CitadelId;
        Assert.NotEqual(luoyangId, zhuoId);

        // 1. At Luoyang: Guan Yu is not in city roster or local nearby list
        var luoyangRoster = NpcLocation.BuildCityNpcs(npcs, society, luoyangId);
        Assert.True(luoyangRoster.Count is > 0 and <= 6);
        Assert.DoesNotContain(luoyangRoster, s => s.NpcId == guanYu.Id);
        Assert.NotNull(luoyangRoster[0].AnimPath);
        var looks = luoyangRoster.Select(s => s.AnimPath ?? s.SpritePath).ToList();
        Assert.Equal(looks.Count, looks.Distinct().Count());

        var localAtLuoyang = npcs.FindByCitadel(luoyangId).First();
        var luoyangTalk = dialogue.BuildTalkScene(localAtLuoyang.Id);
        Assert.DoesNotContain(luoyangTalk.NearbyNpcs, n => n.NpcId == guanYu.Id);

        // 2. At Zhuo County: Guan Yu is present in city roster, empty city returns empty safely
        var zhuoRoster = NpcLocation.BuildCityNpcs(npcs, society, zhuoId);
        Assert.Contains(zhuoRoster, s => s.NpcId == guanYu.Id);
        Assert.Empty(NpcLocation.BuildCityNpcs(npcs, society, 9999));

        // 3. Visiting NPC (home != Luoyang, activity CurrentCitadelId == Luoyang) can chat
        var travelingNpc = npcs.FindByCitadel(zhuoId).First(n => n.Code == "npc_0005");
        var beforeChat = social.ChatWithNpc(travelingNpc.Id);
        Assert.Contains("không có mặt", beforeChat, StringComparison.Ordinal);

        kernel.Db.Execute(
            "UPDATE so_npc_activity SET current_citadel_id = @cit WHERE npc_id = @id",
            new SimT.Contracts.Data.DbArgs().Add("@cit", luoyangId).Add("@id", travelingNpc.Id));

        Assert.Equal(luoyangId, travelingNpc.CurrentCitadelId(society));
        Assert.Equal(zhuoId, travelingNpc.CitadelId);

        var afterChat = social.ChatWithNpc(travelingNpc.Id);
        Assert.DoesNotContain("không có mặt", afterChat, StringComparison.Ordinal);
        Assert.True(social.GetRelationship(travelingNpc.Id).Affinity > 0);
    }
}
