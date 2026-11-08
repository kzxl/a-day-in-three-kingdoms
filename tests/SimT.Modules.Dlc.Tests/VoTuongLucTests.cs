using System.Text.Json;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using SimT.Core.Modules;

namespace SimT.Modules.Dlc.Tests;

public sealed class VoTuongLucTests : IDisposable
{
    private readonly DlcTestHost _host = new();
    private readonly string _customDir = Path.Combine(Path.GetTempPath(), "simt-vtl", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _host.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_customDir))
            Directory.Delete(_customDir, true);
    }

    private static INpcRegistry Npcs(SimKernel k) => k.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
    private static ISocialNetwork Social(SimKernel k) => k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
    private static IDialogueModule Dialogue(SimKernel k) => k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
    private static IPlayerState Player(SimKernel k) => k.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
    private static IPlayerAttributes Attrs(SimKernel k) => k.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
    private static ISocietyChronicle Society(SimKernel k) => k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);

    [Fact]
    public void Npc_is_marked_met_only_after_first_chat_or_dialogue_interaction()
    {
        using var k = _host.Boot();
        var social = Social(k);
        var dialogue = Dialogue(k);
        var playerCity = Player(k).CurrentCityId;
        var localNpcs = Npcs(k).FindByCitadel(playerCity).Take(2).ToList();
        Assert.Equal(2, localNpcs.Count);

        var firstNpc = localNpcs[0];
        var secondNpc = localNpcs[1];

        Assert.False(social.HasMet(firstNpc.Id));
        Assert.Null(social.GetMetNpc(firstNpc.Id));

        // Building a talk scene previews lines without marking the NPC as met
        var preview = dialogue.BuildTalkScene(firstNpc.Id);
        Assert.NotEmpty(preview.Lines);
        Assert.False(social.HasMet(firstNpc.Id));

        // Chatting via SocialService marks firstNpc as met
        social.ChatWithNpc(firstNpc.Id);
        Assert.True(social.HasMet(firstNpc.Id));
        var metFirst = social.GetMetNpc(firstNpc.Id);
        Assert.NotNull(metFirst);
        Assert.Equal(k.Today, metFirst!.FirstMetDate);
        Assert.Equal(playerCity, metFirst.MetCitadelId);

        // Interacting via DialogueService.HandleChoice marks secondNpc as met
        k.AdvanceDays(2);
        Assert.False(social.HasMet(secondNpc.Id));
        dialogue.HandleChoice(secondNpc.Id, "price");
        Assert.True(social.HasMet(secondNpc.Id));
        var metSecond = social.GetMetNpc(secondNpc.Id);
        Assert.NotNull(metSecond);
        Assert.Equal(k.Today, metSecond!.FirstMetDate);
        Assert.Equal(playerCity, metSecond.MetCitadelId);
    }

    [Fact]
    public void Legacy_save_infers_met_npcs_and_earliest_date_accurately()
    {
        using var k = _host.Boot();
        var social = Social(k);
        var all = Npcs(k).All;
        var npcA = all[0];
        var npcB = all[1];
        var npcC = all[2];

        k.Db.Execute("DELETE FROM kz_met_npc; DELETE FROM kz_relationship; DELETE FROM kz_memory;");

        // NPC A: both relationship (affinity != 0) and multiple memories -> earliest date is 184-05-10
        k.Db.Execute(
            "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) VALUES (@id, 30, 60, 'Quen biết', 1, '184-06-15')",
            DbArgs.One("@id", npcA.Id));
        k.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) VALUES (@id, '184-05-25', 'gift', 'positive', 10, 'Tặng trà')",
            DbArgs.One("@id", npcA.Id));
        k.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) VALUES (@id, '184-05-10', 'chat', 'positive', 5, 'Trò chuyện lần đầu')",
            DbArgs.One("@id", npcA.Id));

        // NPC B: memory only -> earliest date is 184-05-18
        k.Db.Execute(
            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) VALUES (@id, '184-05-18', 'chat', 'neutral', 1, 'Gặp gỡ tại quán trọ')",
            DbArgs.One("@id", npcB.Id));

        // NPC C: relationship with affinity == 0 and no memory -> must NOT be inferred as met
        k.Db.Execute(
            "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) VALUES (@id, 0, 50, 'Sơ giao', 0, '184-05-05')",
            DbArgs.One("@id", npcC.Id));

        Assert.True(social.HasMet(npcA.Id));
        Assert.True(social.HasMet(npcB.Id));
        Assert.False(social.HasMet(npcC.Id));

        var recA = social.GetMetNpc(npcA.Id);
        var recB = social.GetMetNpc(npcB.Id);
        Assert.NotNull(recA);
        Assert.NotNull(recB);
        Assert.Equal(new GameDate(184, 5, 10), recA!.FirstMetDate);
        Assert.Equal(new GameDate(184, 5, 18), recB!.FirstMetDate);
        Assert.Null(social.GetMetNpc(npcC.Id));
    }

    [Fact]
    public void Milestones_reward_fame_and_log_chronicle_exactly_once()
    {
        using var k = _host.Boot();
        var social = Social(k);
        var attrs = Attrs(k);
        var npcs = Npcs(k).All;

        var initialFame = attrs.Social.Fame;

        // Meet 10 NPCs -> triggers met_10 once (+20 fame)
        for (var i = 0; i < 10; i++)
            social.RecordMetNpc(npcs[i].Id);

        var fameAfter10 = attrs.Social.Fame;
        Assert.Equal(initialFame + 20, fameAfter10);
        Assert.Equal(1, k.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE stat_impact LIKE 'milestone:met_10%' AND event_kind = 'relationship'"));

        // Re-evaluating milestones or re-meeting same NPCs does not award again
        social.RecordMetNpc(npcs[0].Id);
        social.CheckAndAwardRosterMilestones();
        Assert.Equal(fameAfter10, attrs.Social.Fame);
        Assert.Equal(1, k.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE stat_impact LIKE 'milestone:met_10%'"));

        // Meet up to 50 NPCs -> triggers met_50 once (+50 fame)
        for (var i = 10; i < 50; i++)
            social.RecordMetNpc(npcs[i].Id);

        var fameAfter50 = attrs.Social.Fame;
        Assert.Equal(fameAfter10 + 50, fameAfter50);
        Assert.Equal(1, k.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE stat_impact LIKE 'milestone:met_50%' AND event_kind = 'relationship'"));

        // Meet up to 100 NPCs -> triggers met_100 once (+100 fame)
        for (var i = 50; i < 100; i++)
            social.RecordMetNpc(npcs[i].Id);

        var fameAfter100 = attrs.Social.Fame;
        Assert.Equal(fameAfter50 + 100, fameAfter100);
        Assert.Equal(1, k.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE stat_impact LIKE 'milestone:met_100%' AND event_kind = 'relationship'"));

        // Witness all historical events -> triggers all_events once (+150 fame)
        k.Db.Execute("UPDATE so_historical_event SET status = 'viewed'");
        social.CheckAndAwardRosterMilestones();
        var fameAfterAllEvents = attrs.Social.Fame;
        Assert.Equal(fameAfter100 + 150, fameAfterAllEvents);
        Assert.Equal(1, k.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE stat_impact LIKE 'milestone:all_events%' AND event_kind = 'history'"));

        // Calling CheckAndAwardRosterMilestones again does not duplicate any milestone
        social.CheckAndAwardRosterMilestones();
        Assert.Equal(fameAfterAllEvents, attrs.Social.Fame);
        Assert.Equal(4, social.GetClaimedRosterMilestones().Count);
        Assert.Empty(k.RunInvariants());
    }

    private SimKernel OpenAt(GameDate startDate, string? disabledModule = null)
    {
        var dir = Path.Combine(_customDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "campaign.db");
        var productPath = Path.Combine(dir, "product.json");
        var modules = ModuleLoader.Discover(AppContext.BaseDirectory)
            .Select(d => d.Manifest.Id)
            .Where(id => id != disabledModule)
            .ToArray();

        File.WriteAllText(productPath, JsonSerializer.Serialize(new
        {
            id = "vtl-test",
            name = "Võ Tướng Lục Test",
            seed = 1840502,
            modules,
            startDate = GameDateCodec.Format(startDate),
            startLocation = "lac_duong",
            startingCash = 2000
        }));

        return SimKernel.Open(new KernelOptions(
            dbPath,
            AppContext.BaseDirectory,
            productPath,
            Path.Combine(DlcTestHost.RepoRoot, "assets/config/game-constants.json"),
            BackupDirectory: Path.Combine(dir, "backups")));
    }

    [Fact]
    public void Missed_historical_events_are_distinguished_from_witnessed_and_pending_events()
    {
        using var k = OpenAt(new GameDate(189, 9, 24));
        k.Db.Execute("UPDATE so_historical_event SET day_index = @d WHERE code = 'evt_190_ho_lao_quan'",
            DbArgs.One("@d", k.Today.DayIndex + 2));

        var society = Society(k);

        // Move player to Lạc Dương (citadel 1, related to evt_189_dong_trac_lac_duong) and witness 189-09-25
        Player(k).MoveTo(1, k.Today);
        k.AdvanceDays(1);

        var witnessedPending = society.DequeuePendingHistoricalEvent(Player(k).CurrentCityId);
        Assert.NotNull(witnessedPending);
        Assert.Equal("evt_189_dong_trac_lac_duong", witnessedPending!.Code);
        society.ResolveHistoricalEventChoice("evt_189_dong_trac_lac_duong", "continue");

        // Move player far away to Giao Chỉ (citadel 19) when evt_190_ho_lao_quan triggers on the next day
        Player(k).MoveTo(19, k.Today);
        k.AdvanceDays(1);

        var dossier = DossierSceneBuilder.Build(k.Capabilities);
        Assert.NotNull(dossier);
        Assert.True(dossier!.SocietyEnabled);
        Assert.NotNull(dossier.HistoricalEvents);

        var witnessedView = dossier.HistoricalEvents!.Single(e => e.Code == "evt_189_dong_trac_lac_duong");
        var missedView = dossier.HistoricalEvents!.Single(e => e.Code == "evt_190_ho_lao_quan");
        var futureView = dossier.HistoricalEvents!.Single(e => e.Code == "evt_208_xich_bich_hoa_cong");

        Assert.Equal("viewed", witnessedView.Status);
        Assert.Equal("occurred", missedView.Status);
        Assert.Equal("Không có mặt", missedView.SummaryVi);
        Assert.Equal("pending", futureView.Status);
        Assert.Equal("???", futureView.TitleVi);
        Assert.Equal(208, futureView.Year);
    }

    [Fact]
    public void Disabling_society_dlc_hides_history_tab_without_errors()
    {
        using var k = OpenAt(new GameDate(184, 5, 1), disabledModule: "dlc.society");

        var social = Social(k);
        var npcs = Npcs(k).All;
        for (var i = 0; i < 10; i++)
            social.RecordMetNpc(npcs[i].Id);

        var dossier = DossierSceneBuilder.Build(k.Capabilities);
        Assert.NotNull(dossier);
        Assert.False(dossier!.SocietyEnabled);
        Assert.Empty(dossier.HistoricalEvents!);
        Assert.Equal(10, dossier.MetCount);
        Assert.Empty(k.RunInvariants());
    }
}
