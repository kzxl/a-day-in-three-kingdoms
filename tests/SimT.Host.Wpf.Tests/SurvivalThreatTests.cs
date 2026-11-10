using System.Windows;
using SimT.Contracts.Playing;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf.Tests;

public sealed class SurvivalThreatTests
{
    [Fact]
    public void Evaluate_returns_safe_when_vitals_healthy_and_peaceful()
    {
        var vitals = new CharacterVitals(
            Health: 100,
            MaxHp: 100,
            Fatigue: 20,
            Morale: 80,
            Stress: 15,
            CurrentStamina: 100,
            MaxStamina: 100);

        var result = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 1, isAtWar: false);

        Assert.Equal(SurvivalThreatKind.None, result.Kind);
        Assert.Empty(result.BadgeText);
    }

    [Fact]
    public void Evaluate_detects_critical_hp_at_or_below_thirty_percent()
    {
        var vitals = new CharacterVitals(
            Health: 25,
            MaxHp: 100,
            Fatigue: 10,
            Morale: 50,
            Stress: 10,
            CurrentStamina: 80,
            MaxStamina: 100);

        var result = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 1);

        Assert.Equal(SurvivalThreatKind.CriticalHp, result.Kind);
        Assert.Contains("NGUY CẤP", result.BadgeText);
        Assert.Equal("rest", result.ActionCommand);
        Assert.Equal("#EF4444", result.BorderColorHex);
    }

    [Fact]
    public void Evaluate_detects_severe_fatigue_at_or_above_eighty_percent()
    {
        var vitals = new CharacterVitals(
            Health: 90,
            MaxHp: 100,
            Fatigue: 85,
            Morale: 40,
            Stress: 20,
            CurrentStamina: 10,
            MaxStamina: 100);

        var result = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 1);

        Assert.Equal(SurvivalThreatKind.SevereFatigue, result.Kind);
        Assert.Contains("KIỆT SỨC", result.BadgeText);
        Assert.Equal("rest", result.ActionCommand);
        Assert.Equal("#F59E0B", result.BorderColorHex);
    }

    [Fact]
    public void Evaluate_detects_severe_stress_at_or_above_eighty_percent()
    {
        var vitals = new CharacterVitals(
            Health: 80,
            MaxHp: 100,
            Fatigue: 30,
            Morale: 30,
            Stress: 88,
            CurrentStamina: 70,
            MaxStamina: 100);

        var result = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 1);

        Assert.Equal(SurvivalThreatKind.SevereStress, result.Kind);
        Assert.Contains("TÂM THẦN", result.BadgeText);
        Assert.Equal("rest", result.ActionCommand);
        Assert.Equal("#EC4899", result.BorderColorHex);
    }

    [Fact]
    public void Evaluate_detects_unsafe_territory_when_danger_level_is_three_or_at_war()
    {
        var vitals = new CharacterVitals(
            Health: 100,
            MaxHp: 100,
            Fatigue: 10,
            Morale: 80,
            Stress: 10,
            CurrentStamina: 100,
            MaxStamina: 100);

        var result = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 3, isAtWar: false);

        Assert.Equal(SurvivalThreatKind.UnsafeTerritory, result.Kind);
        Assert.Contains("HIỂM ĐỊA", result.BadgeText);
        Assert.Equal("map", result.ActionCommand);
    }

    [Fact]
    public void Banner_updates_visibility_and_fires_action_event_on_sta_thread()
    {
        WpfTestContext.RunOnSta(() =>
        {
            var banner = new GameSurvivalThreatBanner();
            Assert.Equal(Visibility.Collapsed, banner.Visibility);

            var lowHp = new CharacterVitals(
                Health: 20,
                MaxHp: 100,
                Fatigue: 10,
                Morale: 50,
                Stress: 10,
                CurrentStamina: 50,
                MaxStamina: 100);

            var triggeredAction = string.Empty;
            banner.ThreatActionTriggered += action => triggeredAction = action;

            var assessment = banner.Update(lowHp, dangerLevel: 1);
            Assert.Equal(SurvivalThreatKind.CriticalHp, assessment.Kind);
            Assert.Equal(Visibility.Visible, banner.Visibility);
        });
    }
}
