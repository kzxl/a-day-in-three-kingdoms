using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class PlayerAttributesTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static IPlayerAttributes PlayerAttrs(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

    [Fact]
    public void Protagonist_starts_with_canonical_twelve_attributes()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        var a = attrs.Attributes;
        Assert.Equal(55, a.War);
        Assert.Equal(50, a.Ldr);
        Assert.Equal(60, a.Int);
        Assert.Equal(50, a.Pol);
        Assert.Equal(55, a.Dip);
        Assert.Equal(65, a.Com); // Merchant focus
        Assert.Equal(55, a.Log);
        Assert.Equal(50, a.Cra);
        Assert.Equal(60, a.Con); // Constitution
        Assert.Equal(65, a.Wil); // Willpower
        Assert.Equal(60, a.Prc); // Perception
        Assert.Equal(70, a.Lck); // Fortune
    }

    [Fact]
    public void Protagonist_vitals_are_derived_dynamically()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        var v = attrs.Vitals;
        Assert.Equal(100, v.Health);
        Assert.Equal(0, v.Fatigue);
        Assert.Equal(100, v.Morale);
        Assert.Equal(0, v.Stress);

        // MaxHP = 80 + Con*1.5 + War*0.5 = 80 + 90 + 27 = 197
        Assert.True(v.MaxHp > 150);

        // MaxStamina = 70 + Con*1.2 + War*0.3 + Health/10 = 70 + 72 + 16 + 10 = 168
        Assert.True(v.MaxStamina > 150);
        Assert.Equal(v.MaxStamina, v.CurrentStamina);
    }

    [Fact]
    public void Applying_fatigue_and_resting_mutates_vitals_correctly()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        // Work spends 35 fatigue
        attrs.ApplyFatigue(35);
        Assert.Equal(35, attrs.Vitals.Fatigue);
        Assert.Equal(attrs.Vitals.MaxStamina - 35, attrs.Vitals.CurrentStamina);

        // 2 days of rest recovers 40 fatigue
        attrs.Rest(2);
        Assert.Equal(0, attrs.Vitals.Fatigue);
        Assert.Equal(attrs.Vitals.MaxStamina, attrs.Vitals.CurrentStamina);
    }

    [Fact]
    public void Advancing_days_automatically_recovers_stamina_and_lowers_fatigue()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        // Exhaust player by 50 fatigue
        attrs.ApplyFatigue(50);
        Assert.Equal(50, attrs.Vitals.Fatigue);

        // Advance 1 day -> DayAdvanced event fires -> recovers 3 + Con*0.05 + War*0.02 = 3 + 3 + 1 = 7
        kernel.AdvanceDays(1);

        var v = attrs.Vitals;
        Assert.True(v.Fatigue < 50);
        Assert.Equal(50 - 7, v.Fatigue);
        Assert.Equal(v.MaxStamina - 43, v.CurrentStamina);

        // Advance 7 more days (total 8 days) -> fatigue clears to 0
        kernel.AdvanceDays(7);
        Assert.Equal(0, attrs.Vitals.Fatigue);
        Assert.Equal(attrs.Vitals.MaxStamina, attrs.Vitals.CurrentStamina);
    }

    [Fact]
    public void Gaining_exp_levels_up_stat_when_crossing_hundred()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        var comBefore = attrs.Attributes.Com;

        // Gain 150 COM EXP: should gain +1 COM and 50 residual EXP
        attrs.AddExp("com", 150);

        Assert.Equal(comBefore + 1, attrs.Attributes.Com);
    }

    [Fact]
    public void Protagonist_exposes_attribute_exp_progress_correctly()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        // Initially 0 exp across all attributes
        Assert.Equal(0, attrs.Exps.War);
        Assert.Equal(0, attrs.Exps.Com);
        Assert.Equal(0, attrs.Exps.Prc);

        // Add 45 PRC exp -> 45
        attrs.AddExp("prc", 45);
        Assert.Equal(45, attrs.Exps.Prc);
        Assert.Equal(45, attrs.Exps.GetExp("PRC"));

        // Add 70 more PRC exp -> 115 total -> +1 PRC stat point, residual 15 exp
        var prcBefore = attrs.Attributes.Prc;
        attrs.AddExp("prc", 70);
        Assert.Equal(prcBefore + 1, attrs.Attributes.Prc);
        Assert.Equal(15, attrs.Exps.Prc);
    }

    [Fact]
    public void Protagonist_starts_with_honorable_social_standing()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        var s = attrs.Social;
        Assert.Equal(100, s.Fame);
        Assert.Equal(50, s.Virtue);
        Assert.Equal(50, s.Credibility);
    }
}
