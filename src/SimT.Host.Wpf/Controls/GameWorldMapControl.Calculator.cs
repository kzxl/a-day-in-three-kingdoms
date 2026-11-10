using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

public sealed partial class GameWorldMapControl
{
    public sealed record RouteStepSecurity(
        string FromName,
        string ToName,
        int Days,
        int DangerLevel,
        bool IsChokepoint,
        string? BlockedReasonVi);

    public sealed record TravelSupplyProjection(
        long TargetCityId,
        string TargetCityName,
        TravelMode Mode,
        TravelQuote Quote,
        int GaoQty,
        int MeatQty,
        int TotalRationsAvailable,
        int RationsNeeded,
        int MissingRations,
        long RoadFoodFee,
        long TravelFee,
        long TotalCost,
        long LiquidFunds,
        bool HasEnoughFunds,
        int CurrentFatigue,
        int ProjectedFatigue,
        bool IsFatigueCritical,
        int MaxDangerLevel,
        int CarryCapacity,
        int CurrentCargoUnits,
        IReadOnlyList<RouteStepSecurity> RouteSteps);

    public TravelSupplyProjection ComputeSupplyProjection(long fromCityId, long toCityId, TravelMode mode)
    {
        var targetCity = _map.Find(toCityId);
        var targetName = targetCity?.NameVi ?? $"Thành #{toCityId}";
        var quote = _map.QuoteTravel(fromCityId, toCityId, mode);

        var gaoQty = 0;
        var meatQty = 0;
        var currentCargo = 0;
        var liquidFunds = 0L;
        var currentFatigue = 0;

        if (_kernel != null)
        {
            if (_kernel.Capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag) && bag != null)
            {
                var rows = bag.Rows(fromCityId);
                gaoQty = rows.FirstOrDefault(r => r.ItemId == 1)?.Quantity ?? 0;
                meatQty = rows.FirstOrDefault(r => r.ItemId == 8)?.Quantity ?? 0;
                currentCargo = rows.Sum(r => r.Quantity);
            }

            if (_kernel.Capabilities.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) && wallet != null)
            {
                liquidFunds = wallet.Liquid;
            }

            if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs != null)
            {
                currentFatigue = attrs.Vitals.Fatigue;
            }
        }

        var totalRations = gaoQty + meatQty;
        var (usedGao, usedMeat) = TravelLuggage.ComputeRationsUsed(quote.RationsNeeded, gaoQty, meatQty);
        var missingRations = Math.Max(0, quote.RationsNeeded - usedGao - usedMeat);
        var roadFoodFee = missingRations * 3L;
        var travelFee = quote.TravelFee;
        var totalCost = travelFee + roadFoodFee;
        var hasEnoughFunds = liquidFunds >= totalCost;
        var projectedFatigue = Math.Clamp(currentFatigue + quote.FatigueExpected, 0, 100);
        var isFatigueCritical = projectedFatigue >= 90;

        // Route steps security analysis
        var steps = new List<RouteStepSecurity>();
        var maxDanger = quote.Danger;
        var nodes = quote.PathNodeIds;
        for (var i = 0; i < nodes.Count - 1; i++)
        {
            var n1 = nodes[i];
            var n2 = nodes[i + 1];
            var status = _map.GetRouteStatus(n1, n2);
            var fromName = _map.Find(n1)?.NameVi ?? $"#{n1}";
            var toName = _map.Find(n2)?.NameVi ?? $"#{n2}";
            var days = _map.TravelDays(n1, n2);
            var isChoke = status.Danger >= 2;
            steps.Add(new RouteStepSecurity(fromName, toName, days, status.Danger, isChoke, status.BlockedReasonVi));
        }

        return new TravelSupplyProjection(
            toCityId,
            targetName,
            mode,
            quote,
            gaoQty,
            meatQty,
            totalRations,
            quote.RationsNeeded,
            missingRations,
            roadFoodFee,
            travelFee,
            totalCost,
            liquidFunds,
            hasEnoughFunds,
            currentFatigue,
            projectedFatigue,
            isFatigueCritical,
            maxDanger,
            quote.CarryCapacity,
            currentCargo,
            steps);
    }

    private FrameworkElement CreateSupplyMetricCard(
        string icon,
        string title,
        string mainValue,
        string subValue,
        string statusText,
        Color badgeColor)
    {
        var card = new Border
        {
            Background = (Brush)Application.Current.FindResource("SurfaceAlt"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 6)
        };

        var dock = new DockPanel();

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x33, badgeColor.R, badgeColor.G, badgeColor.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, badgeColor.R, badgeColor.G, badgeColor.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = statusText,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(badgeColor)
        };
        DockPanel.SetDock(badge, Dock.Right);
        dock.Children.Add(badge);

        var stack = new StackPanel();
        var header = new TextBlock
        {
            Text = $"{icon} {title}: {mainValue}",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("Text")
        };
        stack.Children.Add(header);

        if (!string.IsNullOrEmpty(subValue))
        {
            var sub = new TextBlock
            {
                Text = subValue,
                FontSize = 10,
                Foreground = (Brush)Application.Current.FindResource("Muted"),
                Margin = new Thickness(0, 1, 0, 0)
            };
            stack.Children.Add(sub);
        }

        dock.Children.Add(stack);
        card.Child = dock;
        return card;
    }
}
