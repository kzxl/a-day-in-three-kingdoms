using System.Windows;
using SimT.Contracts;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Host.Wpf.Controls;
using SimT.Testing;

namespace SimT.Host.Wpf.Tests;

public sealed class TravelSupplyProjectionTests
{
    [Fact]
    public void Travel_supply_projection_calculates_rations_funds_and_safety_accurately()
    {
        WpfTestContext.RunOnSta(() =>
        {
            using var host = new TestHost("map-projection");
            using var kernel = host.Boot();

            var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
            var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
            var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
            var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

            var fromCityId = player.CurrentCityId;
            var neighbours = map.RouteNeighbours(fromCityId);
            Assert.NotEmpty(neighbours);
            var targetCityId = neighbours.First();

            // 1. Initially bag has no food at current city
            var control = new GameWorldMapControl(map, fromCityId, kernel);
            var projNoFood = control.ComputeSupplyProjection(fromCityId, targetCityId, TravelMode.Walk);

            Assert.Equal(0, projNoFood.TotalRationsAvailable);
            Assert.True(projNoFood.RationsNeeded > 0);
            Assert.Equal(projNoFood.RationsNeeded, projNoFood.MissingRations);
            Assert.Equal(projNoFood.MissingRations * 3L, projNoFood.RoadFoodFee);
            Assert.Equal(projNoFood.RoadFoodFee, projNoFood.TotalCost); // Walk fee is 0

            // 2. Add enough rations (Gao and Meat)
            bag.Add(fromCityId, 1, 10); // 10 Gao
            bag.Add(fromCityId, 8, 5);  // 5 Meat

            var projWithFood = control.ComputeSupplyProjection(fromCityId, targetCityId, TravelMode.Walk);
            Assert.Equal(10, projWithFood.GaoQty);
            Assert.Equal(5, projWithFood.MeatQty);
            Assert.Equal(15, projWithFood.TotalRationsAvailable);
            Assert.Equal(0, projWithFood.MissingRations);
            Assert.Equal(0, projWithFood.RoadFoodFee);
            Assert.Equal(0, projWithFood.TotalCost);
            Assert.True(projWithFood.HasEnoughFunds);

            // 3. Compare Walk vs Horse vs Caravan modes
            var projHorse = control.ComputeSupplyProjection(fromCityId, targetCityId, TravelMode.Horse);
            Assert.True(projHorse.Quote.ActualDays <= projWithFood.Quote.ActualDays);
            Assert.True(projHorse.TravelFee > 0);
            Assert.Equal(TravelLuggage.HorseCapacity, projHorse.CarryCapacity);

            var projCaravan = control.ComputeSupplyProjection(fromCityId, targetCityId, TravelMode.Caravan);
            Assert.True(projCaravan.TravelFee > projHorse.TravelFee);
            Assert.Equal(TravelLuggage.CaravanCapacity, projCaravan.CarryCapacity);

            // 4. Verify route step security breakdown
            Assert.NotEmpty(projWithFood.RouteSteps);
            foreach (var step in projWithFood.RouteSteps)
            {
                Assert.False(string.IsNullOrWhiteSpace(step.FromName));
                Assert.False(string.IsNullOrWhiteSpace(step.ToName));
                Assert.True(step.Days > 0);
                Assert.Equal(step.DangerLevel >= 2, step.IsChokepoint);
            }
        });
    }

    [Fact]
    public void Travel_supply_projection_flags_insufficient_funds_when_wallet_is_empty()
    {
        WpfTestContext.RunOnSta(() =>
        {
            using var host = new TestHost("map-wallet");
            using var kernel = host.Boot();

            var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
            var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
            var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

            var fromCityId = player.CurrentCityId;
            var targetCityId = map.RouteNeighbours(fromCityId).First();

            // Drain wallet
            wallet.Charge(wallet.Liquid, "TEST", "Drain");
            Assert.Equal(0, wallet.Liquid);

            var control = new GameWorldMapControl(map, fromCityId, kernel);
            // Horse mode requires fee > 0
            var projHorse = control.ComputeSupplyProjection(fromCityId, targetCityId, TravelMode.Horse);

            Assert.False(projHorse.HasEnoughFunds);
            Assert.True(projHorse.TotalCost > 0);
        });
    }

    [Fact]
    public void Travel_supply_projection_handles_same_citadel_gracefully()
    {
        WpfTestContext.RunOnSta(() =>
        {
            using var host = new TestHost("map-same");
            using var kernel = host.Boot();

            var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
            var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

            var control = new GameWorldMapControl(map, player.CurrentCityId, kernel);
            var proj = control.ComputeSupplyProjection(player.CurrentCityId, player.CurrentCityId, TravelMode.Walk);

            Assert.Equal(0, proj.RationsNeeded);
            Assert.Equal(0, proj.Quote.ActualDays);
            Assert.Equal(0, proj.TotalCost);
            Assert.Empty(proj.RouteSteps);
        });
    }
}
