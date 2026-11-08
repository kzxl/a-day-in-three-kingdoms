using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

/// <summary>
/// dlc.character — the consolidated character & relationship pillar module.
/// Unifies Social (affinity matrix, retainers, banquets, letters, sworn brothers),
/// Dialogue (branching choices, contextual lines, visits),
/// and Lineage (marriage, offspring, ancestral shrine, tutoring).
/// </summary>
public sealed class CharacterModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var registry = context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var attributes = context.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var bonds = new NpcBondService(context);
        bonds.Initialize();
        context.Capabilities.Provide(INpcBonds.CapabilityId, bonds);
        context.Events.Subscribe<MonthClosed>(month => bonds.OnMonthClosed(month.Date));

        // 1. Social Service
        var socialService = new SocialService(
            context.Db,
            context.Clock,
            wallet,
            bag,
            registry,
            map,
            player,
            attributes,
            items,
            context.Log,
            context.Capabilities);
        socialService.SetCampaignSeed(context.Campaign.Seed);
        context.Capabilities.Provide(CapabilityIds.SocialNetwork, socialService);
        context.Capabilities.Provide(CapabilityIds.NpcTraits, socialService);

        var socialScreen = new SocialScreen(socialService, registry, map, player, bag, items, context.Clock, attributes);
        context.Ui.AddScreen(socialScreen);
        context.Ui.AddCommandHandler(new SocialCommandHandler(socialService, bag, player, items, attributes));
        context.Events.Subscribe<MonthClosed>(month => socialService.OnMonthClosed(month.Date));

        // 2. Dialogue Service
        var dialogueService = new DialogueService(context);
        context.Capabilities.Provide(CapabilityIds.Dialogue, dialogueService);

        // 3. Lineage Service
        var lineageService = new LineageService(context.Db, context.Clock, wallet, context.Rng, context.Ids, context.ActionTime, context.Log);
        context.Capabilities.Provide(CapabilityIds.LineageHouse, lineageService);

        context.Ui.AddScreen(new FamilyScreen(lineageService));
        context.Ui.AddCommandHandler(new FamilyCommandHandler(lineageService));
        context.Events.Subscribe<MonthClosed>(closed => lineageService.ChargeMonthly(closed.Date));

        var wishes = new NpcWishService(context);
        wishes.Initialize();
        context.Capabilities.Provide(INpcWishes.CapabilityId, wishes);
        context.Events.Subscribe<MonthClosed>(month => wishes.OnMonthClosed(month.Date));
        context.Events.Subscribe<DayAdvanced>(day => wishes.Refresh(day.Date));
        context.Events.Subscribe<SimT.Contracts.Events.DuelCompletedEvent>(duel =>
        {
            wishes.OnDuel(duel.NpcId, duel.PlayerWon, duel.Date);
            if (duel.PlayerWon)
                socialService.RecordDuelVictoryBonus(duel.NpcId);
        });

        // Invariants
        foreach (var invariant in CharacterInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"character pillar ready: social network, dialogue system, lineage house (married={lineageService.IsMarried}, children={lineageService.ChildCount})");
    }
}
