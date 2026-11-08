using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class PortraitExpressionTests
{
    [Fact]
    public void Part_a_and_b_portraits_are_distinct_keep_street_sprites_and_fall_back_for_expressions()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var portraits = new HashSet<string>();
        foreach (var number in new[] { 24, 28, 17, 26, 19, 37, 48, 35, 14, 40,
            39, 43, 18, 42, 23, 41, 29, 51, 50, 53,
            54, 49, 52, 38, 36, 44, 45, 46, 47, 30,
            31, 58, 59, 60, 61, 4, 73, 87, 88, 89 })
        {
            var npc = npcs.FindByCode($"npc_{number:0000}")!;
            var art = SceneArtCatalog.ResolveNpcArt(npc);
            var archetype = SceneArtCatalog.ClassifyArchetype(npc);
            var previous = SceneArtCatalog.ResolveArchetypeKeys(archetype);
            Assert.NotEqual($"art/portraits/{previous.PortraitKey}/full.webp", art.PortraitFull);
            Assert.True(portraits.Add(art.PortraitFull), npc.Code);
            Assert.True(art.SpritePath == $"art/sprites/{previous.SpriteKey}.png",
                $"{npc.Code}: expected street sprite {previous.SpriteKey}, got {art.SpritePath}");
            Assert.Equal(art.PortraitFace, SceneArtCatalog.ResolveProfileArt(npc));
            foreach (var expression in new[] { "happy", "angry", "sad" })
                Assert.Equal(art.PortraitFull, SceneArtCatalog.ResolvePortraitExpression(art.PortraitFull, expression));
        }
    }

    [Theory]
    [InlineData("art/portraits/absent/full.webp", "happy")]
    [InlineData("art/portraits/guan_yu/full.webp", "unknown")]
    [InlineData("art/profiles/npc_0205.webp", "sad")]
    [InlineData("art/portraits/guan_yu/full.webp", null)]
    public void Missing_or_unsupported_expression_uses_normal_portrait(string normal, string? expression)
    {
        Assert.Equal(normal, SceneArtCatalog.ResolvePortraitExpression(normal, expression));
    }

    [Theory]
    [InlineData("happy")]
    [InlineData("angry")]
    [InlineData("sad")]
    public void Manifest_resolves_installed_expressions(string expression)
    {
        foreach (var key in new[] { "cao_cao", "commoner", "elder", "farmer_woman", "general", "guan_yu", "lady",
            "liu_bei", "lu_bu", "merchant", "player", "scholar", "swordswoman", "zhang_fei", "zhuge_liang" })
            Assert.Equal($"art/portraits/{key}/full_{expression}.webp",
                SceneArtCatalog.ResolvePortraitExpression($"art/portraits/{key}/full.webp", expression));
    }

    [Fact]
    public void Favorite_gift_is_happy_and_rejected_gift_is_angry_only_on_feedback_line()
    {
        using var host = new DlcTestHost();
        using var kernel = host.Boot();
        var npcs = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npc = npcs.FindByCode("npc_0003")!;
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var catalog = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        kernel.Db.Execute("UPDATE so_npc_activity SET current_citadel_id = @city WHERE npc_id = @id",
            new DbArgs().Add("@city", player.CurrentCityId).Add("@id", npc.Id));
        Assert.Empty(dialogue.BuildTalkScene(npc.Id).LineExpressions);
        var favorite = catalog.All.Single(i => i.Code == "sat");
        bag.Add(player.CurrentCityId, favorite.Id, 2);
        var happy = dialogue.HandleChoice(npc.Id, $"gift:{favorite.Id}");
        Assert.True(happy.Ok, happy.MessageVi);
        AssertFeedback(happy.UpdatedScene!, "happy");
        var grain = catalog.All.Single(i => i.Code == "gao");
        bag.Add(player.CurrentCityId, grain.Id, 2);
        var angry = dialogue.HandleChoice(npc.Id, $"gift:{grain.Id}");
        Assert.False(angry.Ok);
        AssertFeedback(angry.UpdatedScene!, "angry");
        Assert.Equal(2, bag.Quantity(player.CurrentCityId, grain.Id));
        Assert.Empty(dialogue.BuildTalkScene(npc.Id).LineExpressions);
    }

    [Fact]
    public void History_expressions_survive_reopen_without_duplicate_rows()
    {
        using var host = new DlcTestHost();
        long count;
        using (var kernel = host.Boot())
        {
            var history = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
            var evt = history.FindHistoricalEvent("evt_207_tam_co_thao_lu")!;
            Assert.Equal("happy", evt.PortraitExpressions["npc_0001"][0]);
            var battle = history.FindHistoricalEvent("evt_208_xich_bich_hoa_cong")!;
            Assert.Equal(new[] { "angry", "normal", "sad" }, battle.PortraitExpressions["npc_0022"]);
            count = kernel.Db.ScalarLong("SELECT COUNT(*) FROM so_event_portrait_expression", new DbArgs());
            kernel.Save();
        }
        using var reopened = host.Boot();
        Assert.Equal(count, reopened.Db.ScalarLong("SELECT COUNT(*) FROM so_event_portrait_expression", new DbArgs()));
    }

    [Fact]
    public void Archetype_variants_are_deterministic_distinct_across_adjacent_codes_and_preserve_named_or_profile_npcs()
    {
        foreach (var archetype in new[] { "general", "scholar", "merchant", "elder", "commoner", "lady", "farmer_woman", "swordswoman" })
        {
            var a0 = SceneArtCatalog.ResolveNpcArt("npc_0090", archetype);
            var a0Repeat = SceneArtCatalog.ResolveNpcArt("npc_0090", archetype);
            Assert.Equal(a0, a0Repeat);

            var a1 = SceneArtCatalog.ResolveNpcArt("npc_0091", archetype);
            var a2 = SceneArtCatalog.ResolveNpcArt("npc_0092", archetype);
            Assert.Equal($"art/portraits/{archetype}/full.webp", a0.PortraitFull);
            Assert.Equal($"art/portraits/{archetype}_v2/full.webp", a1.PortraitFull);
            Assert.Equal($"art/portraits/{archetype}_v3/full.webp", a2.PortraitFull);
            Assert.Equal(3, new[] { a0.PortraitFull, a1.PortraitFull, a2.PortraitFull }.Distinct().Count());
            Assert.Equal(3, new[] { a0.PortraitFace, a1.PortraitFace, a2.PortraitFace }.Distinct().Count());
        }

        // Named characters with dedicated portraits never change
        Assert.Equal("art/portraits/liu_bei/full.webp", SceneArtCatalog.ResolveNpcArt("npc_0001", "general").PortraitFull);
        Assert.Equal("art/portraits/zhang_fei/full.webp", SceneArtCatalog.ResolveNpcArt("npc_0002", "general").PortraitFull);
        Assert.Equal("art/portraits/guan_yu/full.webp", SceneArtCatalog.ResolveNpcArt("npc_0003", "general").PortraitFull);
        Assert.Equal("art/portraits/cao_cao/full.webp", SceneArtCatalog.ResolveNpcArt("npc_0022", "general").PortraitFull);
        Assert.Equal("art/portraits/zhuge_liang/full.webp", SceneArtCatalog.ResolveNpcArt("npc_0025", "scholar").PortraitFull);
        Assert.Equal("art/portraits/lu_bu/full.webp", SceneArtCatalog.ResolveNpcArt("npc_0057", "general").PortraitFull);

        // Profile-backed NPCs and player stay unchanged
        Assert.Equal("art/profiles/npc_0205.webp", SceneArtCatalog.ResolveNpcArt("npc_0205", "scholar").PortraitFull);
        Assert.Equal(("player", "traveler"), SceneArtCatalog.ResolveArchetypeKeys("player", "npc_0091"));

        // Missing variant falls back to base portrait
        var partialVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "general_v2" };
        Assert.Equal("general_v2", SceneArtCatalog.ResolveArchetypeVariantKey("general", "npc_0091", partialVariants));
        Assert.Equal("general", SceneArtCatalog.ResolveArchetypeVariantKey("general", "npc_0092", partialVariants));
    }

    private static void AssertFeedback(TalkScene scene, string expected)
    {
        Assert.Equal(scene.Lines.Count, scene.LineExpressions.Count);
        var feedback = scene.Lines.Select((line, i) => (line, i)).Single(x => x.line == scene.FeedbackVi);
        Assert.Equal(expected, scene.LineExpressions[feedback.i]);
        Assert.All(scene.LineExpressions.Where((_, i) => i != feedback.i), e => Assert.Equal("normal", e));
    }
}
