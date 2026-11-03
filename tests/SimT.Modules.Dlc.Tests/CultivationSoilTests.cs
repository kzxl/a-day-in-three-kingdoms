using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Modules.Dlc.Cultivation;

namespace SimT.Modules.Dlc.Tests;

/// <summary>
/// Land is finite: every harvest costs soil, poorer soil yields less, fertiliser buys it back
/// and leaving the land alone lets it recover a little each month.
/// </summary>
public sealed class CultivationSoilTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Every_harvest_wears_the_soil_down_by_a_quarter()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Rent(kernel, plot);
        Plant(kernel, plot, "gao");
        GrowAndHarvest(kernel, plot);

        Assert.Equal(CropCalendar.SoilCeiling - CropCalendar.SoilCostPerHarvest, Find(kernel, plot).Soil);
    }

    [Fact]
    public void Poorer_soil_yields_less_on_the_next_crop()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Rent(kernel, plot);
        Plant(kernel, plot, "gao");
        var firstYield = Find(kernel, plot).YieldQuantity!.Value;
        GrowAndHarvest(kernel, plot);

        Plant(kernel, plot, "gao");
        var secondYield = Find(kernel, plot).YieldQuantity!.Value;

        Assert.True(secondYield < firstYield, $"expected {secondYield} < {firstYield}");
    }

    [Fact]
    public void Fertilising_restores_soil_and_charges_for_it()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Rent(kernel, plot);
        Plant(kernel, plot, "gao");
        GrowAndHarvest(kernel, plot);

        var worn = Find(kernel, plot).Soil;
        var before = kernel.Ledger.Balance;

        var message = Command(kernel, $"fertilise:{plot.Id}");

        Assert.Contains("Bón phân", message);
        Assert.Equal(CropCalendar.SoilCeiling, Find(kernel, plot).Soil);
        Assert.Equal(before - CropCalendar.FertiliseCost, kernel.Ledger.Balance);
        Assert.True(worn < CropCalendar.SoilCeiling);
    }

    [Fact]
    public void Full_soil_cannot_be_fertilised_again()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Rent(kernel, plot);

        var message = Command(kernel, $"fertilise:{plot.Id}");

        Assert.Contains("chưa cần bón", message);
    }

    [Fact]
    public void Soil_recovers_a_little_every_month()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Rent(kernel, plot);
        Plant(kernel, plot, "gao");
        GrowAndHarvest(kernel, plot);

        var worn = Find(kernel, plot).Soil;
        kernel.AdvanceDays(31);

        Assert.Equal(worn + CropCalendar.SoilRecoveryPerMonth, Find(kernel, plot).Soil);
    }

    [Fact]
    public void Tea_takes_longer_and_yields_fewer_units_than_rice()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Rent(kernel, plot);

        Plant(kernel, plot, "gao");
        var rice = Find(kernel, plot);
        GrowAndHarvest(kernel, plot);

        Plant(kernel, plot, "tra");
        var tea = Find(kernel, plot);

        Assert.Equal(CropSpec.Tea.FixedGrowthDays, tea.ReadyDayIndex!.Value - kernel.Today.DayIndex);
        Assert.True(tea.YieldQuantity!.Value < rice.YieldQuantity!.Value);
    }

    [Fact]
    public void An_unknown_crop_is_refused()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Rent(kernel, plot);

        var message = Command(kernel, $"plant:{plot.Id}:lua_mach");

        Assert.Contains("Không có giống", message);
        Assert.Null(Find(kernel, plot).CropStatus);
    }

    [Fact]
    public void Invariants_hold_through_soil_exhaustion_and_recovery()
    {
        using var kernel = _host.Boot();
        var plot = HomePlot(kernel);
        Rent(kernel, plot);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            Plant(kernel, plot, "gao");
            GrowAndHarvest(kernel, plot);
            Command(kernel, $"fertilise:{plot.Id}");
        }

        Assert.Empty(kernel.RunInvariants());
        Assert.InRange(Find(kernel, plot).Soil, 0, CropCalendar.SoilCeiling);
    }

    private static void Rent(SimKernel kernel, PlotView plot) =>
        Assert.Contains("Thuê", Command(kernel, $"rent:{plot.Id}"));

    private static void Plant(SimKernel kernel, PlotView plot, string crop) =>
        Assert.Contains("Gieo", Command(kernel, $"plant:{plot.Id}:{crop}"));

    private static void GrowAndHarvest(SimKernel kernel, PlotView plot)
    {
        var ready = Find(kernel, plot).ReadyDayIndex!.Value;
        kernel.AdvanceDays((int)Math.Max(1, ready - kernel.Today.DayIndex));
        Assert.Contains("Gặt", Command(kernel, $"harvest:{plot.Id}"));
    }

    private static PlotView Find(SimKernel kernel, PlotView plot) =>
        Guild(kernel).Plots.Single(candidate => candidate.Id == plot.Id);

    private static PlotView HomePlot(SimKernel kernel) =>
        Guild(kernel).Plots.First(plot => plot.CitadelId == Player(kernel).CurrentCityId);

    private static string Command(SimKernel kernel, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler("cultivation.farm", out var handler), "farm screen is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    private static IPlayerState Player(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

    private static ICultivationEstate Guild(SimKernel kernel) =>
        kernel.Capabilities.Get<ICultivationEstate>(CapabilityIds.CultivationEstate);
}
