using SimT.Modules.Dlc.Economy.Banking;
using SimT.Modules.Dlc.Economy.Caravan;
using SimT.Modules.Dlc.Economy.Shocks;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Economy;

/// <summary>
/// dlc.economy — the consolidated economy pillar module.
/// Unifies Market (trade, warehouses, player stall, dynamic pricing),
/// Caravan (freight, contracts, fleet management),
/// Cultivation (seasonal crops, plots, soil recovery),
/// and Professions (crafting, equipment, mastery tiers, workshops).
/// </summary>
public sealed class EconomyModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var items = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var npcs = context.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var nr) ? nr : null;
        var attributes = context.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attr) ? attr : null;
        var social = context.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var sn) ? sn : null;
        var chronicle = context.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var sc) ? sc : null;

        // 1. Shocks & Arbitrage Engine + Market Service
        var shockEngine = new MarketShockEngine(context.Db);
        context.Capabilities.Provide(CapabilityIds.MarketRumorEvents, (IEventRumorSource)shockEngine);

        var marketService = new MarketService(context.Db, context.Clock, context.ActionTime, wallet, bag, items, map, context.Log, npcs, shockEngine, context.Capabilities);
        marketService.EnsureListings(items, map);
        context.Capabilities.Provide(CapabilityIds.MarketTrade, marketService);
        context.Capabilities.Provide(CapabilityIds.MarketPortfolio, marketService);
        context.Capabilities.Provide(CapabilityIds.MarketWarehouse, marketService);
        context.Capabilities.Provide(CapabilityIds.PlayerStall, marketService);
        context.Capabilities.Provide(CapabilityIds.NpcMarket, marketService);

        // 1b. Value Chain & Civic Consumption Service
        var valueChainService = new SimT.Modules.Dlc.Economy.ValueChain.ValueChainService(context.Db, items, map, npcs, marketService, context.Log, context.Capabilities);
        context.Capabilities.Provide(CapabilityIds.ValueChain, valueChainService);

        // 2. Caravan Service
        var freightPerUnit = context.Config.GetLong("freightPerUnit", CaravanService.DefaultFreightPerUnit);
        var caravanService = new CaravanService(context.Db, context.Clock, wallet, bag, map, items, player, context.Ids, context.Log, context.ActionTime, freightPerUnit, npcs, context.Capabilities);
        context.Capabilities.Provide(CapabilityIds.CaravanGuild, caravanService);

        // 3. Farm / Cultivation Service
        var farmService = new FarmService(context.Db, context.Clock, wallet, bag, items, context.Ids, context.ActionTime, context.Log, context.Capabilities);
        farmService.ValidateCrops();
        farmService.EnsurePlots(map);
        context.Capabilities.Provide(CapabilityIds.CultivationEstate, farmService);

        // 2c. Banking Service
        var bankingService = new BankingService(context.Db, context.Clock, wallet, context.Log);
        var playerBankService = new PlayerBankService(context.Db, context.Clock, wallet, context.Log);

        // 2d. Automated Logistics & Cartel Warfare
        var logisticsService = new AutomatedLogisticsService(context.Db, context.Log);

        // 4. Profession Service
        var professionService = new ProfessionService(context.Db, context.Clock, wallet, bag, items, context.Log, attributes, map, npcs, social, chronicle, player, context.Capabilities);
        professionService.ValidateCatalog();
        context.Capabilities.Provide(CapabilityIds.ProfessionGuild, professionService);
        context.Capabilities.Provide(CapabilityIds.PlayerEquipment, professionService);

        // UI Screens & Command Handlers
        context.Ui.AddScreen(new StallScreen(marketService, marketService, items, map, bag, player, context.Clock, wallet, context.ActionTime, npcs, valueChainService));
        context.Ui.AddScreen(new PlayerStallScreen(marketService, marketService, items, map, bag, player, context.Clock, wallet, context.ActionTime));
        context.Ui.AddScreen(new WarehouseScreen(marketService, items, map, bag, player, context.ActionTime));
        context.Ui.AddCommandHandler(new MarketCommandHandler("market.stall", marketService, player));
        context.Ui.AddCommandHandler(new MarketCommandHandler("market.player_stall", marketService, player));
        context.Ui.AddCommandHandler(new MarketCommandHandler("market.warehouse", marketService, player));

        context.Ui.AddScreen(new CaravanScreen(caravanService, map, bag, player, context.Clock, items, npcs, logisticsService));
        context.Ui.AddCommandHandler(new CaravanCommandHandler(caravanService, player, logisticsService, wallet, map, items));

        context.Ui.AddScreen(new FarmScreen(farmService, map, player, context.Clock));
        context.Ui.AddCommandHandler(new FarmCommandHandler(farmService, player, marketService));

        context.Ui.AddScreen(new BankScreen(bankingService, shockEngine, map, items, marketService, player, wallet, context.Clock));
        context.Ui.AddCommandHandler(new BankCommandHandler(bankingService, player));

        context.Ui.AddScreen(new PlayerBankScreen(playerBankService, map, player, wallet, context.Clock));
        context.Ui.AddCommandHandler(new PlayerBankCommandHandler(playerBankService, player));

        var professionScreen = new ProfessionScreen(professionService, items, player, bag);
        context.Ui.AddScreen(professionScreen);
        context.Ui.AddCommandHandler(new ProfessionCommandHandler(professionService, player, professionScreen));

        // Event Subscriptions
        context.Events.Subscribe<MonthClosed>(closed => marketService.ChargeMonthlyRent(closed.Date));
        context.Events.Subscribe<MonthClosed>(closed => marketService.ChargeMonthlyStallRent(closed.Date));
        context.Events.Subscribe<MonthClosed>(closed => marketService.RestockMonthly(closed.Date));
        context.Events.Subscribe<MonthClosed>(closed =>
        {
            var military = context.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var mc) ? mc : null;
            shockEngine.SimulateMonthlyShocks(closed.Date, map, military);
            foreach (var c in map.Citadels)
            {
                valueChainService.SimulateRegionalExtraction(c.Id);
            }
            valueChainService.SimulateMonthlyCivicConsumption(closed.Date);
        });
        context.Events.Subscribe<DayAdvanced>(day => marketService.SpoilOneDay(day.Date));
        context.Events.Subscribe<DayAdvanced>(day => marketService.DailyRetailSales(day.Date));
        context.Events.Subscribe<DayAdvanced>(day => marketService.RecordDailyPrices(day.Date));

        context.Events.Subscribe<DayAdvanced>(day => caravanService.OnDay(day.Date));

        context.Events.Subscribe<DayAdvanced>(day => farmService.OnDay(day.Date));
        context.Events.Subscribe<MonthClosed>(closed => farmService.ChargeMonthlyRent(closed.Date));
        context.Events.Subscribe<MonthClosed>(closed => farmService.RecoverSoil(closed.Date));

        context.Events.Subscribe<DayAdvanced>(day => professionService.OnDay(day.Date));
        context.Events.Subscribe<MonthClosed>(closed => professionService.OnMonth(closed.Date));
        context.Events.Subscribe<MonthClosed>(_ => bankingService.ProcessMonthlyInterest());
        context.Events.Subscribe<MonthClosed>(closed => playerBankService.SimulateMonthlyTurn(closed.Date));
        context.Events.Subscribe<DayAdvanced>(day => playerBankService.StepDailyConvoys(day.Date));
        context.Events.Subscribe<DayAdvanced>(day => logisticsService.StepAutomatedRoutes(day.Date, marketService, wallet, map, items, caravanService));

        // Invariants
        foreach (var invariant in EconomyInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"economy pillar ready: market({items.All.Count} items), caravan, cultivation({farmService.Plots.Count} plots), professions({professionService.Trades.Count} trades)");
    }
}
