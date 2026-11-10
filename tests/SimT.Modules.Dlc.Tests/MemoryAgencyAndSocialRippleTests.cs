using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class MemoryAgencyAndSocialRippleTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void MemoryDisposition_grudged_npc_marks_up_prices_and_sanctions_military_goods()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npcMarket = kernel.Capabilities.Get<INpcMarket>(CapabilityIds.NpcMarket);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var npc = registry.All.First(n => n.CitadelId == 1);
        var ironItem = items.FindByCode("sat") ?? items.All.First(i => i.Category == "material");

        // Record a betrayal memory and hostile relationship
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, -50, 10, 'Thù địch', 0, '184-05-01')",
            DbArgs.One("@id", npc.Id));

        kernel.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
            "VALUES (@id, '184-05-01', 'betrayal', 'negative', -40, 'Bội ước thương trường khiến đối phương căm hận')",
            DbArgs.One("@id", npc.Id));

        var disp = social.GetDisposition(npc.Id);
        Assert.True(disp.IsGrudged);
        Assert.False(disp.IsFavored);
        Assert.True(disp.PriceFactorPermille >= 1200);

        // Put iron in player bag and register a stall owned by this NPC
        bag.Add(1, ironItem.Id, 5);
        kernel.Db.Execute(
            "INSERT INTO mk_stall (id, citadel_id, sector, name_vi, owner_kind, npc_id, reputation) " +
            "VALUES (991, 1, 'material', 'Thiết Tiệm Cừu Nhân', 'npc', @npc, 50)",
            DbArgs.One("@npc", npc.Id));

        // Attempting to sell material/military goods to grudged NPC is sanctioned/rejected
        var result = npcMarket.SellToNpcStall(1, 991, ironItem.Id, 2);
        Assert.False(result.Ok);
        Assert.Contains("cự tuyệt", result.Message);
    }

    [Fact]
    public void MemoryDisposition_favored_npc_offers_discount()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var npc = registry.All.First(n => n.CitadelId == 1);

        // Record a sworn/favored bond relationship
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, 80, 90, 'Tri kỷ', 5, '184-05-01')",
            DbArgs.One("@id", npc.Id));

        kernel.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
            "VALUES (@id, '184-05-01', 'wish', 'positive', 25, 'Hoàn thành tâm nguyện cứu giúp đại nghiệp')",
            DbArgs.One("@id", npc.Id));

        var disp = social.GetDisposition(npc.Id);
        Assert.False(disp.IsGrudged);
        Assert.True(disp.IsFavored);
        Assert.True(disp.PriceFactorPermille < 1000);
        Assert.Contains("Tri kỷ", disp.SummaryVi);
    }

    [Fact]
    public void BondRipple_gifting_influential_npc_spreads_gratitude_to_allies_and_kin()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var liu = registry.FindByCode("npc_0001")!;
        var guan = registry.FindByCode("npc_0003")!;
        var zhang = registry.FindByCode("npc_0002")!;

        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var liuCityId = liu.CurrentCitadelId(kernel.Capabilities);
        player.MoveTo(liuCityId, kernel.Today);

        var luxuryItem = items.FindByCode("tram_huong") ?? items.FindByCode("tra") ?? items.All.First();
        bag.Add(liuCityId, luxuryItem.Id, 2);

        var initialGuanAffinity = social.GetRelationship(guan.Id).Affinity;
        var initialZhangAffinity = social.GetRelationship(zhang.Id).Affinity;

        // Player gifts Lưu Bị
        var giftResult = social.GiftNpc(liu.Id, luxuryItem.Id);
        Assert.Contains("Đã biếu tặng", giftResult);

        // Check that Quan Vũ and Trương Phi received ripple positive affinity and gratitude memory
        var finalGuan = social.GetRelationship(guan.Id);
        var finalZhang = social.GetRelationship(zhang.Id);

        Assert.True(finalGuan.Affinity > initialGuanAffinity, "Quan Vũ should gain affinity when Lưu Bị is honored.");
        Assert.True(finalZhang.Affinity > initialZhangAffinity, "Trương Phi should gain affinity when Lưu Bị is honored.");

        var guanMemories = social.GetMemories(guan.Id);
        Assert.Contains(guanMemories, m => m.Topic == "bond_gratitude" && m.Sentiment == "positive");

        var zhangMemories = social.GetMemories(zhang.Id);
        Assert.Contains(zhangMemories, m => m.Topic == "bond_gratitude" && m.Sentiment == "positive");
    }

    [Fact]
    public void CaravanRoute_hostile_notables_impose_customs_toll_surcharge()
    {
        using var kernel = _host.Boot();
        var caravan = kernel.Capabilities.Get<ICaravanGuild>(CapabilityIds.CaravanGuild);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var destCity = 2L;
        var destNpc = registry.All.First(n => n.CitadelId == destCity);

        // Load 10 grain in city 1 bag
        bag.Add(1, 1, 10);
        wallet.Pay(1000, "test", "Fund wallet for freight");

        // Record a hostile grudge at destination city
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, -60, 5, 'Kẻ địch', 0, '184-05-01')",
            DbArgs.One("@id", destNpc.Id));
        kernel.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
            "VALUES (@id, '184-05-01', 'feud', 'negative', -30, 'Thù sâu như biển')",
            DbArgs.One("@id", destNpc.Id));

        var sendResult = caravan.Send(1, destCity);
        Assert.True(sendResult.Ok);
        Assert.Contains("quan ải thù địch phụ thu cước", sendResult.Message);
    }

    [Fact]
    public void MemoryDisposition_core_betrayal_and_wish_survive_trivial_chat_flood_and_minor_feuds_decay_over_time()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        var betrayedNpc = registry.All.First(n => n.CitadelId == 1);
        var minorFeudNpc = registry.All.Skip(1).First(n => n.CitadelId == 1);

        // 1. Betrayed NPC has neutral current affinity (-10) but a core 'betrayal' memory followed by 15 trivial 'chat' memories
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, -10, 30, 'Lạnh nhạt', 0, '184-01-01')",
            DbArgs.One("@id", betrayedNpc.Id));

        kernel.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
            "VALUES (@id, '184-01-01', 'betrayal', 'negative', -35, 'Phản bội minh ước')",
            DbArgs.One("@id", betrayedNpc.Id));

        for (var i = 0; i < 15; i++)
        {
            kernel.Db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@id, '184-01-02', 'chat', 'neutral', 1, 'Chào hỏi xã giao')",
                DbArgs.One("@id", betrayedNpc.Id));
        }

        // Core betrayal MUST NOT be evicted by 15 trivial chat entries
        var betrayedDisp = social.GetDisposition(betrayedNpc.Id);
        Assert.True(betrayedDisp.IsGrudged, "Core betrayal memory must survive 15 trivial chat entries.");
        Assert.Equal(1350, betrayedDisp.PriceFactorPermille);

        // 2. Minor feud NPC has neutral affinity (-5) and an old minor 'rival' memory from 5 years ago
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, -5, 45, 'Sơ giao', 0, '184-01-01')",
            DbArgs.One("@id", minorFeudNpc.Id));

        kernel.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
            "VALUES (@id, '178-01-01', 'rival', 'negative', -3, 'Hiềm khích nhỏ năm xưa')",
            DbArgs.One("@id", minorFeudNpc.Id));

        // After 6+ years (2160 days > Ebbinghaus half-life), a minor -3 rival memory should decay below the grudge threshold
        var decayedDisp = social.GetDisposition(minorFeudNpc.Id);
        Assert.False(decayedDisp.IsGrudged, "Old minor rival memory (-3 delta from 6 years ago) should decay via Ebbinghaus retention.");
    }

    [Fact]
    public void BondRipple_major_favor_propagates_two_hops_across_kin_network()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        // Create a 2-hop kin chain: Target (Rank C at player city) --sworn--> Hop1 --mentor--> Hop2
        var target = registry.All.First(n => n.CitadelId == player.CurrentCityId && social.GetNpcRank(n.Id) == NpcRank.C);
        var others = registry.All.Where(n => n.Id != target.Id && social.GetNpcRank(n.Id) == NpcRank.C).Take(2).ToList();
        Assert.Equal(2, others.Count);
        var hop1 = others[0];
        var hop2 = others[1];

        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_npc_bond (npc_a, npc_b, kind, strength, since_date, source) VALUES (@a, @b, 'sworn', 90, '184-01-01', 'history')",
            new DbArgs().Add("@a", Math.Min(target.Id, hop1.Id)).Add("@b", Math.Max(target.Id, hop1.Id)));
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_npc_bond (npc_a, npc_b, kind, strength, since_date, source) VALUES (@a, @b, 'mentor', 85, '184-01-01', 'history')",
            new DbArgs().Add("@a", hop1.Id).Add("@b", hop2.Id));

        var bestItem = items.All.OrderByDescending(i => i.BasePrice).First();
        bag.Add(player.CurrentCityId, bestItem.Id, 2);

        var hop2Before = social.GetRelationship(hop2.Id).Affinity;
        social.GiftNpc(target.Id, bestItem.Id);
        var hop2After = social.GetRelationship(hop2.Id).Affinity;

        Assert.True(hop2After > hop2Before, "Major gift to Rank C NPC should propagate 2-hop gratitude to mentor of sworn brother.");
    }

    [Fact]
    public void DialogueRecall_surfaces_salient_episodic_memory_in_talk_scene()
    {
        using var kernel = _host.Boot();
        var dialogue = kernel.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var npc = registry.All.First(n => n.CitadelId == 1);

        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, 65, 80, 'Tri kỷ', 2, '184-01-05')",
            DbArgs.One("@id", npc.Id));

        kernel.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
            "VALUES (@id, '184-01-05', 'wish', 'positive', 25, 'Hoàn thành tâm nguyện tìm cổ vật')",
            DbArgs.One("@id", npc.Id));

        var scene = dialogue.BuildTalkScene(npc.Id);
        Assert.Contains(scene.Lines, l => l.Contains("tâm nguyện", StringComparison.OrdinalIgnoreCase) || l.Contains("ân tình", StringComparison.OrdinalIgnoreCase));
        Assert.All(scene.Lines, l => Assert.True(l.Length <= 90));
    }

    [Fact]
    public void SocietyPlanning_favored_strategist_prioritizes_player_citadel_when_seeking_patron()
    {
        using var kernel = _host.Boot();
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var playerCityId = player.CurrentCityId;
        var neighbourCityId = map.RouteNeighbours(playerCityId).First();

        // Pick any non-ruler NPC at the neighboring city and set their focus_path to 'strategist'
        var strategist = registry.All.First(n =>
            !n.Role.Contains("Hoàng Đế")
            && !n.Role.Contains("Quân Chủ")
            && !n.Role.Contains("Thái Thú")
            && !n.Role.Contains("Thái Sư")
            && !n.Role.Contains("Giáo Tổ"));
        registry.UpdateCitadel(strategist.Id, neighbourCityId);

        // Ensure strategist has met and strongly favors the player, and has high power/fame needs
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_met_npc (npc_id, first_met_date, met_citadel_id) VALUES (@id, '184-01-01', @cit)",
            new DbArgs().Add("@id", strategist.Id).Add("@cit", neighbourCityId));
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, 85, 90, 'Tri kỷ', 3, '184-01-01')",
            DbArgs.One("@id", strategist.Id));
        kernel.Db.Execute(
            "UPDATE so_npc_activity SET current_citadel_id = @cit, focus_path = 'strategist', current_wealth = 500 WHERE npc_id = @id",
            new DbArgs().Add("@id", strategist.Id).Add("@cit", neighbourCityId));
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO so_npc_needs (npc_id, safety_need, wealth_need, fame_need, power_need, social_need, updated_date) " +
            "VALUES (@id, 10, 10, 95, 95, 50, '184-01-01')",
            DbArgs.One("@id", strategist.Id));
        kernel.Db.Execute("DELETE FROM so_npc_goal WHERE npc_id = @id", DbArgs.One("@id", strategist.Id));

        // Advance months until strategist rolls SeekPatron (50% chance vs ScholarTour)
        NpcGoalDef? seekGoal = null;
        for (var m = 0; m < 6; m++)
        {
            kernel.Db.Execute("DELETE FROM so_npc_goal WHERE npc_id = @id", DbArgs.One("@id", strategist.Id));
            kernel.AdvanceDays(30);
            var g = society.GetNpcGoal(strategist.Id);
            if (g?.GoalType == "SeekPatron")
            {
                seekGoal = g;
                break;
            }
        }

        Assert.NotNull(seekGoal);
        Assert.Equal(playerCityId, seekGoal!.TargetCitadelId);
        Assert.Contains("tri âm", seekGoal.PlanDesc);
    }

    [Fact]
    public void SocialScreen_renders_active_memory_disposition_and_salient_memory_in_dossier_details()
    {
        using var kernel = _host.Boot();
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var localNpc = registry.All.First(n => n.CitadelId == player.CurrentCityId);

        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_met_npc (npc_id, first_met_date, met_citadel_id) VALUES (@id, '184-01-02', @cit)",
            new DbArgs().Add("@id", localNpc.Id).Add("@cit", player.CurrentCityId));
        kernel.Db.Execute(
            "INSERT OR REPLACE INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
            "VALUES (@id, 75, 85, 'Tri kỷ', 2, '184-01-02')",
            DbArgs.One("@id", localNpc.Id));
        kernel.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
            "VALUES (@id, '184-01-02', 'wish', 'positive', 25, 'Hoàn thành tâm nguyện tìm bảo kiếm')",
            DbArgs.One("@id", localNpc.Id));

        var screen = kernel.Ui.Screens.First(s => s.ScreenId == "social.network");
        var view = screen.Render();
        var localSection = view.Sections.First(s => s.Heading.Contains("Nhân Sĩ Hữu Duyên Cùng Thành"));
        var npcRow = localSection.Rows.First(r => r.Title != null && r.Title.Contains(localNpc.NameVi));

        Assert.NotNull(npcRow.Details);
        Assert.Contains(npcRow.Details!, d => d.Label == "Thái Độ & Ưu Đãi" && d.Value.Contains("Trọng vọng"));
        Assert.Contains(npcRow.Details!, d => d.Label == "Ký Ức Khắc Cốt" && d.Value.Contains("Hoàn thành tâm nguyện tìm bảo kiếm"));
    }
}
