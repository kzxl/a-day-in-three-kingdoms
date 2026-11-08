using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class PortraitExpressionTests
{
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

    private static void AssertFeedback(TalkScene scene, string expected)
    {
        Assert.Equal(scene.Lines.Count, scene.LineExpressions.Count);
        var feedback = scene.Lines.Select((line, i) => (line, i)).Single(x => x.line == scene.FeedbackVi);
        Assert.Equal(expected, scene.LineExpressions[feedback.i]);
        Assert.All(scene.LineExpressions.Where((_, i) => i != feedback.i), e => Assert.Equal("normal", e));
    }
}
