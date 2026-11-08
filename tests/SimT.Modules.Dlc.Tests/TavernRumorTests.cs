using SimT.Contracts.Data;
using SimT.Contracts.Events;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class TavernRumorTests : IDisposable
{
    private readonly DlcTestHost _host = new();
    public void Dispose() => _host.Dispose();
    private static ITavernRumors Rumors(SimKernel k) => k.Capabilities.Get<ITavernRumors>(CapabilityIds.TavernRumors);
    private static IPlayerState Player(SimKernel k) => k.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

    [Fact]
    public void True_price_is_the_largest_live_deviation_and_matches_the_market()
    {
        using var k = _host.Boot();
        var news = Rumors(k).Listen();
        Assert.Equal(3, news.Count);
        Assert.All(news, r => Assert.StartsWith("Nghe nói…", r.NarrativeVi));
        var price = Assert.Single(news, r => r.Kind == "price");
        var storedTruth = k.Db.ScalarLong("SELECT is_true FROM so_rumor WHERE id=@id", DbArgs.One("@id", price.Id));
        // Fixed kernel seed makes this fixture a true price rumor.
        Assert.Equal(1, storedTruth);
        var market = k.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var items = k.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var map = k.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var item = items.Find(price.ItemId!.Value)!;
        var quote = market.QuoteBuy(price.TargetCityId!.Value, item.Id);
        Assert.Contains($"{quote} quan (giá gốc {item.BasePrice} quan)", price.NarrativeVi);
        var deviation = Math.Abs((double)quote / item.BasePrice - 1);
        foreach (var city in map.Citadels.Where(c => c.Id != Player(k).CurrentCityId))
            foreach (var good in items.All.Where(i => i.BasePrice > 0))
                Assert.True(deviation >= Math.Abs((double)market.QuoteBuy(city.Id, good.Id) / good.BasePrice - 1));
    }

    [Fact]
    public void Repeat_within_ten_days_and_reload_preserve_batch_money_and_time()
    {
        RumorView[] first;
        long balance;
        using (var k = _host.Boot())
        {
            var wallet = k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
            var before = wallet.Balance;
            var beforeDay = k.Today;
            Assert.Equal(TavernRules.CostDays, k.ActionTime.Quote(TavernRules.ActionCode).Cost.Days);
            first = Rumors(k).Listen().ToArray();
            balance = wallet.Balance;
            Assert.Equal(TavernRules.CostQuan, before - balance);
            Assert.Equal(beforeDay, k.Today);
            k.AdvanceDays(9);
            Assert.Equal(first.Select(r => r.Id), Rumors(k).Listen().Select(r => r.Id));
            Assert.Equal(balance, wallet.Balance);
            Assert.Equal(beforeDay.AddDays(9), k.Today);
            k.Save();
        }
        using var reopened = _host.Boot();
        Assert.Equal(first.Select(r => r.Id), Rumors(reopened).Listen().Select(r => r.Id));
        Assert.Equal(balance, reopened.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet).Balance);
    }

    [Fact]
    public void Expiry_removes_card_news_but_keeps_verified_muted_history_and_refreshes()
    {
        using var k = _host.Boot();
        var first = Rumors(k).Listen();
        k.AdvanceDays(10);
        Assert.Empty(Rumors(k).ActiveAt(Player(k).CurrentCityId));
        Assert.All(Rumors(k).History(), r => Assert.NotNull(r.VerifiedTruth));
        k.Ui.TryGetCommandHandler("society.chronicle", out var commands);
        commands!.Execute("filter:rumors");
        var model = k.Ui.Screens.Single(s => s.ScreenId == "society.chronicle").Render();
        Assert.All(model.Sections.Single().Rows, r => Assert.True(r.IsMuted));
        Assert.DoesNotContain(Rumors(k).Listen(), r => first.Any(old => old.Id == r.Id));
    }

    [Fact]
    public void Arrival_verifies_price_and_whereabouts_and_retains_false_outcome()
    {
        using var k = _host.Boot();
        var news = Rumors(k).Listen();
        foreach (var rumor in news.Where(r => r.Kind is "price" or "whereabouts"))
        {
            k.Capabilities.Get<IEventPublisher>(CapabilityIds.EventPublisher).Publish(
                new PlayerRelocatedEvent(1, Player(k).CurrentCityId, rumor.TargetCityId!.Value, k.Today));
            var actual = Rumors(k).History().Single(r => r.Id == rumor.Id);
            Assert.NotNull(actual.VerifiedTruth);
            Assert.Equal(k.Db.ScalarLong("SELECT is_true FROM so_rumor WHERE id=@id", DbArgs.One("@id", rumor.Id)) == 1,
                actual.VerifiedTruth);
        }
    }

    [Fact]
    public void Fixed_seed_false_rate_tracks_constant_on_large_sample()
    {
        using var k = _host.Boot();
        var broker = new SimT.Core.Modules.CapabilityBroker();
        broker.Provide(CapabilityIds.PlayerWallet, k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet));
        var service = new SimT.Modules.Dlc.Society.SocietyService(k.Db, k.Clock,
            k.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry),
            k.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap), Player(k), new SilentLog(), broker);
        service.SetRumorContext(new SimT.Core.Identity.SplitMix64Rng(1840502), new NoTime());
        k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet).Pay(50000, "test", "Test");
        for (var i = 0; i < 600; i++)
        {
            service.Listen();
            k.Db.Execute("UPDATE so_rumor SET expires_day=0 WHERE expires_day>0");
        }
        var count = k.Db.ScalarLong("SELECT COUNT(*) FROM so_rumor");
        var falseCount = k.Db.ScalarLong("SELECT COUNT(*) FROM so_rumor WHERE is_true=0");
        Assert.Equal(1800, count);
        Assert.InRange((double)falseCount / count, TavernRules.FalseProbability - 0.035,
            TavernRules.FalseProbability + 0.035);
    }

    [Fact]
    public void Optional_society_hides_tavern_and_handles_missing_sublocations()
    {
        Assert.Null(TavernScene.Hotspot([], false));
        Assert.Equal("society.tavern", TavernScene.Hotspot([], true)!.TargetScreenId);
        var inn = new SublocationDef(1, 1, "inn", "Quán trọ ven sông", "inn");
        Assert.Equal(inn.NameVi, TavernScene.Hotspot([inn], true)!.Kind);
    }

    [Fact]
    public void Disabled_society_boots_without_rumors_or_tavern_command()
    {
        var product = Path.Combine(Path.GetDirectoryName(_host.DatabasePath)!, "without-society.json");
        File.WriteAllText(product, System.Text.Json.JsonSerializer.Serialize(new
        {
            id = "no-society", name = "No society", seed = 1840502, startLocation = "lac_duong", startingCash = 2000,
            modules = SimT.Core.Modules.ModuleLoader.Discover(AppContext.BaseDirectory)
                .Select(m => m.Manifest.Id).Where(id => id != "dlc.society").ToArray()
        }));
        using var k = SimKernel.Open(new KernelOptions(_host.DatabasePath, AppContext.BaseDirectory,
            product, SimT.Testing.TestHost.ConstantsPath, BackupDirectory: Path.Combine(Path.GetDirectoryName(product)!, "backups")));
        Assert.False(k.Capabilities.IsProvided(CapabilityIds.TavernRumors));
        var locations = k.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap).Sublocations(Player(k).CurrentCityId);
        Assert.Null(TavernScene.Hotspot(locations, k.Capabilities.IsProvided(CapabilityIds.TavernRumors)));
        k.AdvanceDays(1);
        Assert.Empty(k.RunInvariants());
    }

    [Fact]
    public void Insufficient_money_does_not_create_or_charge_rumors()
    {
        using var k = _host.Boot();
        var wallet = k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        wallet.Charge(wallet.Liquid, "test", "Test");
        var before = k.Today;
        Assert.Throws<SimT.Contracts.SimException>(() => Rumors(k).Listen());
        Assert.Empty(Rumors(k).History());
        Assert.Equal(before, k.Today);
        Assert.Equal(0, wallet.Liquid);
    }

    [Fact]
    public void Sources_use_current_npc_locations_and_real_danger_routes()
    {
        using var k = _host.Boot();
        var society = k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var registry = k.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var map = k.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet).Pay(5000, "test", "Test");
        var kinds = new HashSet<string>();
        for (var i = 0; i < 20; i++)
        {
            var news = Rumors(k).Listen();
            foreach (var r in news)
            {
                kinds.Add(r.Kind);
                var truth = k.Db.ScalarLong("SELECT is_true FROM so_rumor WHERE id=@id", DbArgs.One("@id", r.Id)) == 1;
                if (r.Kind == "whereabouts")
                {
                    var npc = registry.Find(r.NpcId!.Value)!;
                    if (truth) Assert.Equal(npc.CurrentCitadelId(society), r.TargetCityId);
                    else Assert.NotEqual(npc.CurrentCitadelId(society), r.TargetCityId);
                }
                if (r.Kind == "danger" && truth)
                    Assert.Contains(map.Routes, route => route.ToCitadelId == r.TargetCityId);
            }
            k.Db.Execute("UPDATE so_rumor SET expires_day=0 WHERE expires_day>0");
        }
        Assert.Contains("whereabouts", kinds);
        Assert.Contains("danger", kinds);
    }

    [Fact]
    public void Market_event_source_exposes_scheduled_and_active_shocks_and_verifies_occurrence()
    {
        using var k = _host.Boot();
        var day = k.Today.DayIndex;
        var city = Player(k).CurrentCityId;
        var engine = new SimT.Modules.Dlc.Economy.Shocks.MarketShockEngine(k.Db);
        engine.TriggerShock(city, "DROUGHT", (int)day + 2, 5, 1500, "Hạn hán làm chợ khan lương");
        var source = k.Capabilities.Get<IEventRumorSource>(CapabilityIds.MarketRumorEvents);
        var hint = Assert.Single(source.Upcoming(day, 30).Where(h => h.NarrativeVi == "Hạn hán làm chợ khan lương"));
        Assert.False(source.HasOccurred(hint.Key, day));
        Assert.True(source.HasOccurred(hint.Key, day + 2));
        k.Db.Execute("""
            INSERT INTO so_rumor(heard_city_id,heard_day,expires_day,kind,title_vi,narrative_vi,is_true,
                target_city_id,event_key,event_day)
            VALUES(@city,@day,@end,'event','Chợ sắp biến động','Nghe nói… hạn hán',1,@city,@key,@eventDay)
            """, new DbArgs().Add("@city", city).Add("@day", day).Add("@end", day + 10)
                .Add("@key", hint.Key).Add("@eventDay", day + 2));
        Assert.Null(Rumors(k).History().Single().VerifiedTruth);
        k.AdvanceDays(2);
        Assert.True(Rumors(k).History().Single().VerifiedTruth);
    }

    [Fact]
    public void Rumor_migration_is_repeatable_without_losing_heard_news()
    {
        using var k = _host.Boot();
        var first = Rumors(k).Listen().Select(r => r.Id).ToArray();
        var sql = File.ReadAllText(Path.Combine(DlcTestHost.RepoRoot,
            "src/SimT.Modules.Dlc.Society/migrations/0303-society-rumors.sql"));
        k.Db.Execute(sql);
        k.Db.Execute(sql);
        Assert.Equal(first, Rumors(k).Listen().Select(r => r.Id));
    }

    private sealed class NoTime : IActionTime
    {
        public ActionTimeQuote Quote(string code) => new(code, ActionCost.Free);
        public void Charge(string code) { }
    }

    private sealed class SilentLog : SimT.Contracts.Logging.ILog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? error = null) { }
    }
}
