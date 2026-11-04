using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Core.Data;
using SimT.Core.Kernel;
using SimT.Modules.Dlc.Caravan;

namespace SimT.Modules.Dlc.Tests;

/// <summary>
/// Exercises the freight DLC through the same surface the UI uses: the screen command handler.
/// If a rule cannot be reached this way, it is not reachable by a player either.
/// </summary>
public sealed class CaravanTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Freight_leaves_the_city_and_charges_per_unit()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        Seed(kernel, 4);

        var before = kernel.Ledger.Balance;
        var result = Command(kernel, $"send:{Neighbour(kernel)}");

        Assert.Contains("lên đường", result);
        Assert.Equal(0, Bag(kernel).Quantity(player.CurrentCityId, 1));
        Assert.Equal(before - (4 * CaravanService.DefaultFreightPerUnit), kernel.Ledger.Balance);
        Assert.Contains(
            kernel.Ledger.Recent(50),
            line => line.JournalCode == JournalCodes.CaravanFreight);
    }

    [Fact]
    public void Goods_appear_in_the_destination_city_after_the_route_duration()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var destination = Neighbour(kernel);
        var days = Map(kernel).TravelDays(player.CurrentCityId, destination);
        Seed(kernel, 3);

        Command(kernel, $"send:{destination}");
        Assert.Equal(0, Bag(kernel).Quantity(destination, 1));

        kernel.AdvanceDays(days);

        Assert.Equal(3, Bag(kernel).Quantity(destination, 1));
        var run = Guild(kernel).Runs.Single();
        Assert.Equal("ARRIVED", run.Status);
        Assert.Equal(days, run.ArriveDayIndex - run.DepartDayIndex);
        Assert.Equal(0, Guild(kernel).UnitsInTransit);
    }

    [Fact]
    public void Arrival_happens_once_and_never_duplicates_goods()
    {
        using var kernel = _host.Boot();
        var destination = Neighbour(kernel);
        var days = Map(kernel).TravelDays(Player(kernel).CurrentCityId, destination);
        Seed(kernel, 2);

        Command(kernel, $"send:{destination}");
        kernel.AdvanceDays(days + 40);

        Assert.Equal(2, Bag(kernel).Quantity(destination, 1));
    }

    [Fact]
    public void Sending_an_empty_bag_is_refused()
    {
        using var kernel = _host.Boot();

        var result = Command(kernel, $"send:{Neighbour(kernel)}");

        Assert.Contains("Túi trống", result);
        Assert.Empty(Guild(kernel).Runs);
    }

    [Fact]
    public void Sending_to_a_city_without_a_road_is_refused()
    {
        using var kernel = _host.Boot();
        Seed(kernel, 1);

        var result = Command(kernel, "send:3");

        Assert.Contains("Không có đường", result);
        Assert.Empty(Guild(kernel).Runs);
    }

    [Fact]
    public void Freight_costs_more_than_the_wallet_allows()
    {
        using var kernel = _host.Boot();
        Seed(kernel, 4);

        // Drain the wallet below the freight bill by moving cash into the vault.
        kernel.Ledger.Move(kernel.Today, kernel.Ledger.Balance - 10, toSafe: true, JournalCodes.SafeDeposit, "cất két");

        var result = Command(kernel, $"send:{Neighbour(kernel)}");

        Assert.Contains("cước", result);
        Assert.Equal(4, Bag(kernel).Quantity(Player(kernel).CurrentCityId, 1));
    }

    [Fact]
    public void Walking_moves_the_player_and_costs_the_route_duration()
    {
        using var kernel = _host.Boot();
        var from = Player(kernel).CurrentCityId;
        var to = Neighbour(kernel);
        var days = Map(kernel).TravelDays(from, to);
        var before = kernel.Today;

        var result = Command(kernel, $"goto:{to}");

        Assert.Contains("tới nơi", result);
        Assert.Equal(to, Player(kernel).CurrentCityId);
        Assert.Equal(before.AddDays(days), kernel.Today);
    }

    [Fact]
    public void Perishable_cargo_loses_part_of_its_load_on_the_road()
    {
        using var kernel = _host.Boot();
        var destination = Neighbour(kernel);
        var days = Map(kernel).TravelDays(Player(kernel).CurrentCityId, destination);
        Seed(kernel, 20);

        Command(kernel, $"send:{destination}");
        kernel.AdvanceDays(days);

        // grain: shelf life 60 days, transit 3 → 20 * 3 / 60 = 1 lost
        Assert.Equal(19, Bag(kernel).Quantity(destination, 1));
        Assert.Equal(1, Guild(kernel).Runs.Single().Lost);
    }

    [Fact]
    public void Durable_cargo_arrives_whole()
    {
        using var kernel = _host.Boot();
        var destination = Neighbour(kernel);
        var days = Map(kernel).TravelDays(Player(kernel).CurrentCityId, destination);
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        trade.Buy(Player(kernel).CurrentCityId, 4, 2);

        Command(kernel, $"send:{destination}");
        kernel.AdvanceDays(days);

        Assert.Equal(2, Bag(kernel).Quantity(destination, 4));
        Assert.Equal(0, Guild(kernel).Runs.Single().Lost);
    }

    [Fact]
    public void Invariants_stay_clean_through_a_full_freight_cycle()
    {
        using var kernel = _host.Boot();
        var destination = Neighbour(kernel);
        var days = Map(kernel).TravelDays(Player(kernel).CurrentCityId, destination);

        Seed(kernel, 5);
        Command(kernel, $"send:{destination}");
        kernel.AdvanceDays(days);
        Command(kernel, $"goto:{destination}");

        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Disabling_the_dlc_leaves_its_tables_and_rows_in_the_save()
    {
        using (var kernel = _host.Boot())
        {
            Seed(kernel, 2);
            Command(kernel, $"send:{Neighbour(kernel)}");
            kernel.Save();
        }

        // Rows survive because a disabled module is simply not loaded; nothing deletes data.
        using var db = SqliteSession.Open(_host.DatabasePath);
        Assert.Equal(1, db.ScalarLong("SELECT COUNT(*) FROM cv_run"));
        Assert.Equal(2, db.ScalarLong("SELECT SUM(quantity) FROM cv_cargo"));
    }

    [Fact]
    public void Accepting_and_delivering_freight_contract_pays_reward_on_time()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var player = Player(kernel);

        // Contract 1 is from Citadel 1 (Lạc Dương) to Citadel 2 (Trường An), item 2 (Trà), quantity 8, reward 450
        var contract1 = guild.Contracts.First(c => c.Id == 1);
        Assert.Equal("AVAILABLE", contract1.Status);
        Assert.Equal(player.CurrentCityId, contract1.FromCityId);

        var balanceBefore = kernel.Ledger.Balance;
        var acceptMsg = Command(kernel, "accept:1");
        Assert.Contains("Đã nhận ủy thác", acceptMsg);

        // Goods added to bag at starting citadel
        Assert.Equal(8, Bag(kernel).Quantity(player.CurrentCityId, 2));

        // Travel to destination city (Citadel 2)
        var travelMsg = Command(kernel, $"goto:{contract1.ToCityId}");
        Assert.Contains("tới nơi", travelMsg);
        Assert.Equal(contract1.ToCityId, player.CurrentCityId);

        // Deliver contract
        var deliverMsg = Command(kernel, "deliver:1");
        Assert.Contains("Giao tiêu thành công", deliverMsg);
        Assert.Contains("450 Quan", deliverMsg);

        // Check wallet and ledger
        Assert.Equal(balanceBefore + 450, kernel.Ledger.Balance);
        var updatedContract = guild.Contracts.First(c => c.Id == 1);
        Assert.Equal("DELIVERED", updatedContract.Status);
        Assert.Equal(0, Bag(kernel).Quantity(player.CurrentCityId, 2));
    }

    [Fact]
    public void Delivering_freight_contract_late_imposes_fifty_percent_penalty()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var player = Player(kernel);

        var contract = guild.Contracts.First(c => c.Id == 1);
        Command(kernel, $"accept:{contract.Id}");

        // Travel to destination first with goods
        Command(kernel, $"goto:{contract.ToCityId}");

        // Advance beyond the deadline (e.g. 30 days later)
        kernel.AdvanceDays(30);

        var deliverMsg = Command(kernel, $"deliver:{contract.Id}");

        Assert.Contains("trễ hạn", deliverMsg);
        Assert.Contains($"{contract.RewardCopper / 2} Quan", deliverMsg);
    }

    [Fact]
    public void Accepting_freight_contract_from_wrong_city_is_refused()
    {
        using var kernel = _host.Boot();
        // Contract 4 is from Citadel 8 (Tương Dương), but player starts at Citadel 1 (Lạc Dương)
        var acceptMsg = Command(kernel, "accept:4");
        Assert.Contains("yêu cầu tiếp nhận tại", acceptMsg);
    }

    [Fact]
    public void Delivering_freight_contract_without_sufficient_goods_is_refused()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        Command(kernel, "accept:1");

        // Discard or remove some contract goods from bag before delivering
        Bag(kernel).Remove(player.CurrentCityId, 2, 5); // remove 5 of 8

        Command(kernel, "goto:2");
        var deliverMsg = Command(kernel, "deliver:1");

        Assert.Contains("Không đủ số lượng", deliverMsg);
    }

    [Fact]
    public void Fleet_defaults_to_tier_1_wooden_cart()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);

        var fleet = guild.Fleet;
        Assert.Equal(1, fleet.Tier);
        Assert.Contains("Xe Thồ Mộc", fleet.NameVi);
        Assert.Equal(1.0, fleet.CapacityMult);
        Assert.Equal(0.0, fleet.CostDiscount);
    }

    [Fact]
    public void Upgrading_fleet_deducts_fee_and_advances_tier()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        wallet.Pay(2000, JournalCodes.CaravanReward, "Thêm ngân lượng thử nghiệm");
        var balanceBefore = kernel.Ledger.Balance;

        // Tier 1 -> Tier 2: Cost 600
        var res1 = guild.UpgradeFleet();
        Assert.True(res1.Ok, res1.Message);
        Assert.Equal(balanceBefore - 600, kernel.Ledger.Balance);
        Assert.Equal(2, guild.Fleet.Tier);
        Assert.Contains("Thiết Xa", guild.Fleet.NameVi);

        // Tier 2 -> Tier 3: Cost 1500
        var res2 = guild.UpgradeFleet();
        Assert.True(res2.Ok, res2.Message);
        Assert.Equal(balanceBefore - 600 - 1500, kernel.Ledger.Balance);
        Assert.Equal(3, guild.Fleet.Tier);
        Assert.Contains("Đại Vận Thuyền", guild.Fleet.NameVi);

        // Max tier check
        var res3 = guild.UpgradeFleet();
        Assert.False(res3.Ok);
        Assert.Contains("đẳng cấp tối cao", res3.Message);
    }

    [Fact]
    public void Send_with_escort_fee_charges_escort_cost_and_records_escort_id()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var player = Player(kernel);
        var neighbour = Neighbour(kernel);
        Seed(kernel, 5);

        var balanceBefore = kernel.Ledger.Balance;
        var res = guild.SendWithEscortFee(player.CurrentCityId, neighbour, 50);
        Assert.True(res.Ok, res.Message);

        var run = guild.Runs.First();
        Assert.NotNull(run.EscortNpcId);
        Assert.True(kernel.Ledger.Balance < balanceBefore);
    }

    private static void Seed(SimKernel kernel, int quantity)
    {
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var player = Player(kernel);
        var outcome = trade.Buy(player.CurrentCityId, 1, quantity);
        Assert.True(outcome.Ok, outcome.Message);
    }

    private static string Command(SimKernel kernel, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler("caravan.routes", out var handler), "caravan screen is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    private static long Neighbour(SimKernel kernel) => Map(kernel).RouteNeighbours(Player(kernel).CurrentCityId)[0];

    private static IPlayerState Player(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

    private static IBag Bag(SimKernel kernel) =>
        kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);

    private static IWorldMap Map(SimKernel kernel) =>
        kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);

    private static ICaravanGuild Guild(SimKernel kernel) =>
        kernel.Capabilities.Get<ICaravanGuild>(CapabilityIds.CaravanGuild);
}
